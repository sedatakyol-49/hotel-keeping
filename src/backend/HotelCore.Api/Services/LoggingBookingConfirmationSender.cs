using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Options;
using HotelCore.Application.Common.Security;
using Microsoft.Extensions.Options;

namespace HotelCore.Api.Services;

/// <summary>
/// §312f BGB onayının <b>geliştirme</b> implementasyonu: e-posta göndermez, belgeyi üretir,
/// özetini hesaplar ve gönderimi loglar.
///
/// <para><b>Neden gerçek bir gönderici yok:</b> SMTP/ESP seçimi bir altyapı <b>ve veri koruma</b>
/// kararıdır (alıcı verisinin üçüncü tarafa aktarımı). Bu fazda soyutlama, akış ve <b>zorunlu
/// içerik</b> sabitlenir; taşıyıcı sonra takılır.</para>
///
/// <para><b>Alıcı adresi log'a maskelenerek yazılır</b> — maskeleme kuralı Application
/// katmanındaki <see cref="EmailMasking"/> ile <b>aynıdır</b>, iki farklı maske olmaz.</para>
/// </summary>
public sealed class LoggingBookingConfirmationSender(
    ILogger<LoggingBookingConfirmationSender> logger,
    IOptions<PublicChannelOptions> options,
    TimeProvider timeProvider)
    : IBookingConfirmationSender
{
    public string Channel => "Email";

    public Task<BookingConfirmationResult> SendAsync(
        BookingConfirmationMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var link = BuildAccessLink(message.Culture, message.HotelSlug, message.AccessToken);

        // Onay belgesinin TAMAMI: §312f zorunlu kalemleri GÖVDEDEDİR, yalnızca bağlantı değil.
        var document = string.Create(
            CultureInfo.InvariantCulture,
            $"{message.Body}\nAccess link: {link}\n");

        logger.BookingConfirmationSent(message.BookingReference, EmailMasking.Mask(message.RecipientEmail));

        // "Ne gönderildi" sorusunun kanıtı: belgenin SHA-256'sı PublicBooking'e yazılır.
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document)));

        return Task.FromResult(new BookingConfirmationResult(timeProvider.GetUtcNow(), hash));
    }

    public Task SendAccessLinkAsync(BookingAccessLinkMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Bağlantı yeniden gönderimi hiçbir rezervasyon verisi taşımaz.
        logger.BookingConfirmationSent(message.BookingReference, EmailMasking.Mask(message.RecipientEmail));

        return Task.CompletedTask;
    }

    private string BuildAccessLink(string culture, string hotelSlug, string accessToken) =>
        options.Value.AccessLinkTemplate
            .Replace("{culture}", culture, StringComparison.Ordinal)
            .Replace("{hotelSlug}", hotelSlug, StringComparison.Ordinal)
            .Replace("{accessToken}", accessToken, StringComparison.Ordinal);
}
