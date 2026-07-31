using HotelCore.Api.Startup;
using HotelCore.Application.Common.Security;
using HotelCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.Services;

/// <summary>
/// Süresi dolmuş ve tüketilmiş <c>BookingHold</c> kayıtlarının arka plan süpürücüsü
/// (architecture-public-booking.md §5.2).
///
/// <para><b>Neden fiziksel silme:</b> çakışma kısıtının
/// (<c>EX_BookingHolds_NoOverlappingActiveHolds</c>) kısmi predikatı <b>immutable</b> olmak
/// zorundadır, yani içinde <c>now()</c> geçemez. Dolayısıyla "süresi dolmuş" hâli predikatla
/// ifade edilemez ve soft-delete edilmiş satır odayı <b>sonsuza dek</b> bloke ederdi.</para>
///
/// <para><b>Süpürücü tek koruma değildir:</b> hold oluşturma handler'ı da aynı transaction'da
/// ilgili oda tipi + kesişen aralık için süresi dolmuş kayıtları siler. Süpürücü, hiç istek
/// gelmeyen oda tiplerini ve tüketilmiş kayıtları temizler.</para>
///
/// <para><b>Çok örnekli çalışma:</b> iki örnek aynı satırı silmeye çalışabilir. Silme
/// <b>idempotenttir</b> ve satır kilidi PostgreSQL tarafındadır; EF'in "0 satır etkilendi"
/// eşzamanlılık istisnası yakalanır ve tur atlanır — bir sonraki tur kalanı temizler. Ayrıca her
/// örnek rastgele bir gecikmeyle başlar, böylece turlar üst üste binmez.</para>
///
/// <para><b>Tenant filtresi bypass edilmez:</b> <c>BookingHold</c> tenant-scoped'dır, bu yüzden
/// süpürücü otel otel dolaşır ve her otelin kapsamını <see cref="PublicTenantScope.Enter"/> ile
/// kurar. <c>IgnoreQueryFilters()</c> arka planda da kullanılmaz — bir kez açılan bypass, ileride
/// kopyalanacak bir örnek olurdu.</para>
/// </summary>
public sealed class PublicHoldSweeper(
    IServiceScopeFactory scopeFactory,
    ILogger<PublicHoldSweeper> logger,
    TimeProvider timeProvider)
    : BackgroundService
{
    /// <summary>Tur aralığı.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>Süresi dolan hold bu kadar bekletildikten sonra silinir (teşhis payı).</summary>
    private static readonly TimeSpan ExpiredGrace = TimeSpan.FromHours(1);

    /// <summary>Tüketilmiş hold bu kadar sonra silinir (destek için geri iz).</summary>
    private static readonly TimeSpan ConsumedGrace = TimeSpan.FromHours(24);

    /// <summary>Tek turda silinecek en fazla satır (uzun kilit tutmamak için).</summary>
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Normal kapanış hata olarak raporlanmamalıdır: Task.Delay ve PeriodicTimer iptalde
        // OperationCanceledException fırlatır ve yakalanmazsa host "BackgroundService failed" der.
        try
        {
            // Örnekler aynı anda başlamasın: eşzamanlılık istisnalarının çoğu böyle önlenir.
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(5, 45)), stoppingToken)
                .ConfigureAwait(false);

            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    await SweepAsync(stoppingToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Süpürücü hiçbir hatada durmamalıdır; bir sonraki tur yeniden dener.
                catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    logger.PublicHoldSweepFailed(exception);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantScope = scope.ServiceProvider.GetRequiredService<PublicTenantScope>();

        // Hotel tenant-scoped DEĞİLDİR (tenant kökünün kendisidir), bu yüzden kapsam kurmadan
        // okunabilir; hold'lar ise otel otel, kapsam içinde temizlenir.
        var hotelIds = await database.Hotels
            .AsNoTracking()
            .Where(hotel => hotel.PublicSlug != null)
            .Select(hotel => hotel.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var expiredBefore = now - ExpiredGrace;
        var consumedBefore = now - ConsumedGrace;

        var expiredCount = 0;
        var consumedCount = 0;

        foreach (var hotelId in hotelIds)
        {
            using (tenantScope.Enter(hotelId))
            {
                var stale = await database.BookingHolds
                    .Where(hold =>
                        (hold.ConsumedAt == null && hold.ExpiresAt < expiredBefore)
                        || (hold.ConsumedAt != null && hold.ConsumedAt < consumedBefore))
                    .OrderBy(hold => hold.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (stale.Count == 0)
                {
                    continue;
                }

                expiredCount += stale.Count(hold => hold.ConsumedAt is null);
                consumedCount += stale.Count(hold => hold.ConsumedAt is not null);

                database.BookingHolds.RemoveRange(stale);

                try
                {
                    await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Başka bir örnek aynı satırları zaten sildi. Silme idempotenttir: bu tur
                    // atlanır, kalan varsa bir sonraki tur temizler.
                    database.ChangeTracker.Clear();
                }
            }
        }

        if (expiredCount + consumedCount > 0)
        {
            logger.PublicHoldsSwept(expiredCount, consumedCount);
        }
    }
}
