namespace HotelCore.Domain.Enums;

/// <summary>
/// Public kanalda sözleşmenin <b>ne zaman kurulduğu</b>
/// (architecture-public-booking.md §10, madde 3 — insan onayı bekleyen hukuki karar).
/// <para>
/// Bu bir <b>ayar</b>dır çünkü cevabı otelin ticari tercihine bağlıdır ve buton metnini,
/// onay e-postasının hukuki niteliğini ve rezervasyonun başlangıç durumunu birlikte belirler.
/// Koda gömülemez.
/// </para>
/// </summary>
public enum PublicBookingConfirmationMode
{
    /// <summary>
    /// Anında onay: onay e-postası <i>Annahme</i>'dir, sözleşme rezervasyon anında kurulur.
    /// Rezervasyon <c>Confirmed</c> olarak yazılır.
    /// </summary>
    Instant = 0,

    /// <summary>
    /// Otel kabulüne bağlı: misafire gönderilen ilk e-posta yalnızca <i>Zugangsbestätigung</i>
    /// (talebin alındığı bildirimi) olur; sözleşme otel kabul edince kurulur.
    /// </summary>
    OnHotelAcceptance = 1
}
