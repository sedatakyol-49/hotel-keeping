using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Otelin iptal politikası — <b>owned type</b> (<c>Hotels</c> tablosunda
/// <c>CancellationPolicy_*</c> kolonları).
/// <para>
/// <b>Kurtaxe politikanın matrahına GİRMEZ</b> (api-contracts-public-booking.md §4.3
/// <c>appliesToAccommodationOnly</c>): konaklama gerçekleşmediği için şehir vergisi hiç doğmaz
/// (<c>CityTaxLiability.ArisesFrom</c> ile aynı kural). Bu bir <i>ayar</i> değil bir
/// <i>değişmez</i>dir, bu yüzden kolon olarak saklanmaz — saklanırsa "kapatılabilir" görünür ve
/// kapatıldığında vergi hukuku ihlal edilirdi.
/// </para>
/// <para>
/// Ücretsiz iptal son tarihi burada saklanmaz, <b>hesaplanır</b>:
/// <c>CheckIn − FreeCancellationDaysBeforeArrival</c> gününde <see cref="CutoffLocalTime"/>,
/// <c>Hotel.TimeZoneId</c> ile mutlak ana çevrilir. Saklanan bir tarih, otel politikasını
/// değiştirdiğinde eski rezervasyonlarla tutarsızlaşırdı; ayrıca rezervasyon anındaki politika
/// zaten <c>PublicBooking.CancellationPolicySnapshotJson</c>'a dondurulur.
/// </para>
/// </summary>
public sealed class CancellationPolicy
{
    /// <summary>Politika türü — misafire gösterilen etiket ve metin anahtarı.</summary>
    public CancellationPolicyType Type { get; set; } = CancellationPolicyType.Flexible;

    /// <summary>
    /// Girişten kaç gün öncesine kadar ücretsiz iptal edilebilir. <c>0</c> = giriş gününün
    /// <see cref="CutoffLocalTime"/> saatine kadar ücretsiz.
    /// </summary>
    public int FreeCancellationDaysBeforeArrival { get; set; } = 3;

    /// <summary>
    /// Son tarihteki kesim saati (otelin <b>yerel</b> saati). Yaz/kış saati geçişlerinde
    /// yorumlanması <c>Hotel.TimeZoneId</c> ile yapılır — bu yüzden saat dilimi kolonu zorunludur.
    /// </summary>
    public TimeOnly CutoffLocalTime { get; set; } = new(18, 0);

    /// <summary>Geç iptal ücreti, konaklama tutarının yüzdesi (0–100).</summary>
    public decimal LateCancellationFeePercent { get; set; } = 90.00m;

    /// <summary>No-show ücreti, konaklama tutarının yüzdesi (0–100).</summary>
    public decimal NoShowFeePercent { get; set; } = 90.00m;
}
