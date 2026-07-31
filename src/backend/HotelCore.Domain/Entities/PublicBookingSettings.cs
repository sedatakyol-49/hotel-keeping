using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Otelin misafire açık (public) rezervasyon kanalı ayarları — <b>owned type</b>
/// (<c>Hotels</c> tablosunda <c>PublicBookingSettings_*</c> kolonları).
/// <para>
/// <b>Neden owned type, ayrı tablo değil:</b> ayarlar otelle bire-bir ve otelsiz anlamsızdır;
/// mevcut <see cref="TaxProfile"/> deseniyle aynı gerekçe (architecture.md §4.1). Her public
/// istek otel satırını zaten okur — ayrı tablo her istekte fazladan bir JOIN olurdu.
/// </para>
/// <para>
/// <b><see cref="IsEnabled"/> varsayılanı <c>false</c> bilinçlidir:</b> mevcut oteller migration
/// sonrası <b>kapalı</b> gelir. Kanal açılması hukuki bir eylemdir (Impressum, AGB, aydınlatma
/// metni yayımlamak gerekir) — şema değişikliğinin yan etkisi olarak açılamaz.
/// </para>
/// </summary>
public sealed class PublicBookingSettings
{
    /// <summary>
    /// Kanal açık mı. <c>false</c> ise otelin slug'ı bilinse bile tüm public uçlar <b>404</b>
    /// döner (otelin varlığı sızdırılmaz — api-contracts-public-booking.md §2.2).
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>En az konaklama süresi (gece).</summary>
    public int MinNights { get; set; } = 1;

    /// <summary>En fazla konaklama süresi (gece).</summary>
    public int MaxNights { get; set; } = 30;

    /// <summary>Bugünden itibaren kaç gün ileriye rezervasyon alınabilir.</summary>
    public int MaxAdvanceDays { get; set; } = 365;

    /// <summary>
    /// Girişten en az kaç saat önce rezervasyon kapanır. <c>0</c> = aynı gün rezervasyon serbest.
    /// </summary>
    public int MinAdvanceHours { get; set; }

    /// <summary>Formda seçilebilecek en fazla yetişkin sayısı.</summary>
    public int MaxAdults { get; set; } = 10;

    /// <summary>Formda seçilebilecek en fazla çocuk sayısı.</summary>
    public int MaxChildren { get; set; } = 10;

    /// <summary>Sözleşmenin kurulma anı — bkz. <see cref="PublicBookingConfirmationMode"/>.</summary>
    public PublicBookingConfirmationMode ConfirmationMode { get; set; } =
        PublicBookingConfirmationMode.Instant;
}
