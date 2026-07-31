using System.Collections.Concurrent;
using System.Globalization;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace HotelCore.Api.Services;

/// <summary>
/// <see cref="IPublicRateLimiter"/>'ın <b>süreç içi</b> sabit pencere (fixed window) sayacı.
///
/// <para><b>Neden ASP.NET Core'un yerleşik rate limiter'ı değil:</b> sözleşme iki farklı
/// bölümleme ekseni ister — uç bazında <c>(hotelSlug, IP)</c> ve <c>POST /bookings</c> için
/// ayrıca <c>(hotelSlug, SHA-256(e-posta))</c>. İkincisi ancak gövde çözümlendikten sonra, yani
/// <b>handler'ın içinde</b> bilinir; yerleşik limiter ise middleware seviyesinde çalışır. İki
/// ayrı mekanizma kullanmak aynı kuralın iki yerde ayrışmasına yol açardı, bu yüzden tek bir
/// port arkasında tek bir sayaç vardır.</para>
///
/// <para><b>Bilinen sınır (bilinçli):</b> sayaç süreç içidir. Çok örnekli dağıtımda etkin sınır
/// örnek sayısıyla çarpılır. Paylaşılan bir depo (Redis) bir altyapı kararıdır ve bu fazın
/// kapsamında değildir; <see cref="IPublicRateLimiter"/> portu o geçişte <b>tek</b> değişecek
/// yerdir.</para>
///
/// <para><b>Bellek:</b> girdiler penceresi dolduğunda periyodik taramayla temizlenir — aksi hâlde
/// her yeni IP kalıcı bir sözlük girdisi bırakırdı ve sınırın kendisi bir bellek tüketim
/// vektörü olurdu.</para>
///
/// <para><b>Neden <see cref="TimeProvider"/>:</b> sınır gerçek zamana bağlıdır ve servis
/// singleton'dır; scoped bir saat portunu enjekte etmek captive dependency üretirdi.</para>
/// </summary>
public sealed class PublicRateLimiter(
    IOptions<PublicChannelSettings> settings,
    TimeProvider timeProvider)
    : IPublicRateLimiter
{
    /// <summary>
    /// Sayaç anahtarının parçalarını ayıran ASCII "unit separator" (U+001F). Yazdırılabilir bir
    /// ayraç (örn. <c>|</c>) bölümleme anahtarının içinde geçebilir ve iki farklı kuralın
    /// sayacını çakıştırabilirdi.
    /// </summary>
    private const char KeySeparator = (char)0x1F;

    /// <summary>Bu kadar erişimde bir, süresi dolmuş girdiler temizlenir.</summary>
    private const int CleanupInterval = 512;

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    private int _accessCount;

    public bool TryAcquire(string bucket, string partitionKey, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (!settings.Value.RateLimits.TryGetValue(bucket, out var rule) || rule.PermitLimit <= 0)
        {
            // Kural tanımlı değilse sınır UYGULANMAZ. Sessizce bir varsayılan uydurmak,
            // yapılandırmadaki bir eksikliği gizler ve üretimde sürpriz 429'lar üretirdi.
            return true;
        }

        MaybeCleanup();

        var now = timeProvider.GetUtcNow();
        var window = TimeSpan.FromSeconds(Math.Max(1, rule.WindowSeconds));
        var key = string.Create(CultureInfo.InvariantCulture, $"{bucket}{KeySeparator}{partitionKey}");

        var state = _windows.AddOrUpdate(
            key,
            _ => new Window(now + window, 1),
            (_, existing) => existing.ExpiresAt <= now
                ? new Window(now + window, 1)
                : existing with { Count = existing.Count + 1 });

        if (state.Count <= rule.PermitLimit)
        {
            return true;
        }

        var remaining = state.ExpiresAt - now;
        retryAfter = remaining > TimeSpan.Zero ? remaining : TimeSpan.FromSeconds(1);

        return false;
    }

    private void MaybeCleanup()
    {
        if (Interlocked.Increment(ref _accessCount) % CleanupInterval != 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        foreach (var pair in _windows)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _windows.TryRemove(pair);
            }
        }
    }

    private sealed record Window(DateTimeOffset ExpiresAt, int Count);
}
