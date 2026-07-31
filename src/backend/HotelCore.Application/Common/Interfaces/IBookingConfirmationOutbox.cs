namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Onay e-postasının <b>outbox</b> kuyruğu (§312f BGB, architecture-public-booking.md §9.8).
/// <para>
/// <b>Neden handler doğrudan göndermez:</b> gönderim transaction dışında olmalıdır. Handler
/// <c>SaveChanges</c>'ten <b>sonra</b> buraya bir kayıt bırakır; taşıyıcı servis kuyruğu ayrı bir
/// kapsamda (scope) boşaltır. Böylece SMTP hatası rezervasyonu geri almaz ve istek gönderimi
/// beklemez.
/// </para>
/// <para>
/// <b>Bilinen sınır (bilinçli):</b> bu fazdaki kuyruk <b>süreç içidir</b>. Süreç onay
/// gönderilmeden düşerse kayıt kaybolur ve rezervasyon <c>ConfirmationSentAt = null</c> ile
/// kalır — yani eksiklik <b>görünürdür</b> ve elle telafi edilebilir. Kalıcı bir outbox tablosu
/// şema değişikliği gerektirir ve bu fazın kapsamında değildir.
/// </para>
/// </summary>
public interface IBookingConfirmationOutbox
{
    /// <summary>Onay gönderimini kuyruğa alır. <b>Asla istisna fırlatmaz.</b></summary>
    void Enqueue(BookingConfirmationMessage message);

    /// <summary>Erişim bağlantısının yeniden gönderimini kuyruğa alır.</summary>
    void Enqueue(BookingAccessLinkMessage message);
}
