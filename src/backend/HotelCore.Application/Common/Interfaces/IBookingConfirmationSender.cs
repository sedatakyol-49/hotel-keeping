namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// §312f BGB — onayın <b>kalıcı veri taşıyıcısında</b> (e-posta) iletilmesi.
/// <para>
/// <b>Rezervasyonla aynı transaction'da çağrılmaz.</b> Kayıt önce commit edilir, gönderim
/// sonradan outbox üzerinden yapılır: SMTP hatası hukuken kurulmuş bir sözleşmeyi geri
/// almamalıdır. Gönderilemeyen onay bir operasyon sorunudur, rezervasyonun yokluğu değil.
/// </para>
/// <para>
/// Zorunlu içerik (architecture-public-booking.md §9.8) <b>gövdededir</b>, yalnızca bağlantı
/// değil: otel künyesi, referans, oda tipi/kişi sayısı, tarihler ve yerel saatler, KDV dâhil
/// toplam ve kırılım (Kurtaxe ayrı satır), ödeme şekli, iptal politikası ve <b>mutlak</b>
/// ücretsiz iptal son tarihi, cayma hakkının bulunmadığı bildirimi, AGB versiyonu, iptal
/// bağlantısı.
/// </para>
/// </summary>
public interface IBookingConfirmationSender
{
    /// <summary>Gönderim kanalı adı (<c>Email</c>).</summary>
    string Channel { get; }

    /// <summary>Onay belgesini gönderir; gönderilen belgenin özetini ve versiyonunu döner.</summary>
    Task<BookingConfirmationResult> SendAsync(
        BookingConfirmationMessage message,
        CancellationToken cancellationToken);

    /// <summary>
    /// Kayıp erişim bağlantısını yeniden gönderir (<c>POST /bookings/lookup</c>).
    /// <b>Yanıt hiçbir bilgi taşımaz</b> — bu metot yalnızca eşleşme bulunduğunda çağrılır.
    /// </summary>
    Task SendAccessLinkAsync(BookingAccessLinkMessage message, CancellationToken cancellationToken);
}

/// <summary>Onay e-postasının içerik girdisi (§312f zorunlu kalemleri).</summary>
/// <param name="PublicBookingId">Gönderim sonucunun yazılacağı kayıt.</param>
/// <param name="HotelId">Otel (tenant kapsamı yeniden kurulur).</param>
/// <param name="HotelSlug">Erişim bağlantısının URL parçası.</param>
/// <param name="BookingReference">Misafire gösterilen referans.</param>
/// <param name="AccessToken">Ham erişim token'ı — <b>yalnızca</b> e-posta bağlantısında görünür.</param>
/// <param name="RecipientEmail">Alıcı.</param>
/// <param name="Culture">Belgenin dili.</param>
/// <param name="DocumentVersion">Onay belgesi şablon versiyonu.</param>
/// <param name="Body">Şablonun ürettiği metin (özeti bu içerikten alınır).</param>
public sealed record BookingConfirmationMessage(
    Guid PublicBookingId,
    Guid HotelId,
    string HotelSlug,
    string BookingReference,
    string AccessToken,
    string RecipientEmail,
    string Culture,
    string DocumentVersion,
    string Body);

/// <summary>Erişim bağlantısının yeniden gönderimi.</summary>
public sealed record BookingAccessLinkMessage(
    Guid PublicBookingId,
    string HotelSlug,
    string BookingReference,
    string AccessToken,
    string RecipientEmail,
    string Culture);

/// <summary>Gönderim sonucu — "ne gönderildi" sorusunun kanıtı.</summary>
/// <param name="SentAt">Gönderim anı.</param>
/// <param name="DocumentHash">Gönderilen belgenin SHA-256 özeti.</param>
public sealed record BookingConfirmationResult(DateTimeOffset SentAt, string DocumentHash);
