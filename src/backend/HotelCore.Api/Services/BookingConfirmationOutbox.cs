using System.Threading.Channels;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Security;
using HotelCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.Services;

/// <summary>
/// §312f onay gönderiminin <b>outbox</b> kuyruğu ve taşıyıcısı.
///
/// <para><b>Neden transaction dışında:</b> rezervasyon commit edildikten <i>sonra</i> gönderilir.
/// SMTP hatası hukuken kurulmuş bir sözleşmeyi geri almamalıdır; gönderilemeyen onay bir
/// operasyon sorunudur, rezervasyonun yokluğu değil. Bu yüzden
/// <see cref="BookingConfirmationOutbox.Enqueue(BookingConfirmationMessage)"/> <b>asla istisna
/// fırlatmaz</b>: kuyruk doluysa kayıt düşürülür ve durum <c>ConfirmationSentAt = null</c> olarak
/// <b>görünür</b> kalır.</para>
///
/// <para><b>Bilinen sınır (bilinçli):</b> kuyruk <b>süreç içidir</b>. Süreç onay gönderilmeden
/// düşerse kayıt kaybolur — ama eksiklik veritabanında görünür ve elle telafi edilebilir. Kalıcı
/// bir outbox tablosu şema değişikliği gerektirir ve bu fazın kapsamında değildir.</para>
/// </summary>
public sealed class BookingConfirmationOutbox : IBookingConfirmationOutbox
{
    /// <summary>Kuyruk kapasitesi; dolduğunda en eski kayıt düşürülür (istek asla bloke olmaz).</summary>
    private const int Capacity = 1024;

    private readonly Channel<object> _channel = Channel.CreateBounded<object>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    /// <summary>Taşıyıcı servisin okuduğu uç.</summary>
    public ChannelReader<object> Reader => _channel.Reader;

    public void Enqueue(BookingConfirmationMessage message) => _channel.Writer.TryWrite(message);

    public void Enqueue(BookingAccessLinkMessage message) => _channel.Writer.TryWrite(message);
}

/// <summary>
/// Outbox kuyruğunu boşaltan arka plan servisi.
///
/// <para>Her kayıt <b>kendi DI kapsamında</b> işlenir: gönderim sonucunu yazmak için yeni bir
/// <c>DbContext</c> gerekir (isteğin kapsamı çoktan kapanmıştır) ve tenant kapsamı
/// <see cref="PublicTenantScope.Enter"/> ile ilgili otele daraltılır — arka plan işi de global
/// query filter'ı <b>bypass etmez</b>.</para>
///
/// <para><b>Hata yutulur ve loglanır:</b> gönderim başarısızsa <c>ConfirmationSentAt</c> boş
/// kalır. Bu bilinçlidir: rezervasyon geçerlidir ve eksiklik sorgulanabilir bir hâl olarak
/// durur.</para>
/// </summary>
public sealed class BookingConfirmationDispatcher(
    BookingConfirmationOutbox outbox,
    IServiceScopeFactory scopeFactory,
    ILogger<BookingConfirmationDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kapanışta ReadAllAsync OperationCanceledException fırlatır. Yakalanmazsa host bunu
        // "BackgroundService failed" olarak görür ve (varsayılan StopHost davranışıyla) çıkışı
        // hata gibi raporlar — normal bir kapanış hata olarak görünmemelidir.
        try
        {
            await foreach (var message in outbox.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await DispatchAsync(message, stoppingToken).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Taşıyıcı hiçbir hatada durmamalıdır; kayıt loglanır ve devam edilir.
                catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    logger.BookingConfirmationFailed(Describe(message), exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }
    }

    private async Task DispatchAsync(object message, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IBookingConfirmationSender>();

        if (message is BookingAccessLinkMessage accessLink)
        {
            await sender.SendAccessLinkAsync(accessLink, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (message is not BookingConfirmationMessage confirmation)
        {
            return;
        }

        var result = await sender.SendAsync(confirmation, cancellationToken).ConfigureAwait(false);

        var tenantScope = scope.ServiceProvider.GetRequiredService<PublicTenantScope>();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Tenant kapsamı ilgili otele DARALTILIR; IgnoreQueryFilters kullanılmaz.
        using (tenantScope.Enter(confirmation.HotelId))
        {
            var booking = await database.PublicBookings
                .FirstOrDefaultAsync(row => row.Id == confirmation.PublicBookingId, cancellationToken)
                .ConfigureAwait(false);

            if (booking is null)
            {
                return;
            }

            booking.ConfirmationSentAt = result.SentAt;
            booking.ConfirmationDocumentHash = result.DocumentHash;
            booking.ConfirmationDocumentVersion = confirmation.DocumentVersion;
            booking.ConfirmationCulture = confirmation.Culture;

            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string Describe(object message) => message switch
    {
        BookingConfirmationMessage confirmation => confirmation.BookingReference,
        BookingAccessLinkMessage accessLink => accessLink.BookingReference,
        _ => "(bilinmiyor)"
    };
}
