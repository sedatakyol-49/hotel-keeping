using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Ücretsiz iptalin <b>mutlak</b> son tarihinin ve iptal ücretinin hesaplandığı <b>tek</b> yer
/// (api-contracts-public-booking.md §4.3).
///
/// <para><b>Hesap:</b> <c>checkIn − freeCancellationDaysBeforeArrival</c> gününde
/// <c>cutoffLocalTime</c>, otelin saat dilimiyle mutlak ana çevrilir. Yaz/kış saati geçişlerinin
/// ele alınışı <see cref="PublicTimeZone.ToInstant"/> içindedir.</para>
///
/// <para><b>Ücret matrahı yalnızca konaklama tutarıdır; Kurtaxe GİRMEZ.</b> Bu bir ayar değil bir
/// değişmezdir: konaklama gerçekleşmediği için şehir vergisi hiç doğmaz
/// (<see cref="CityTaxLiability"/> ile aynı kural). Kolon olarak saklanmaz — saklansaydı
/// "kapatılabilir" görünür ve kapatıldığında vergi hukuku ihlal edilirdi.</para>
/// </summary>
internal static class PublicCancellationService
{
    /// <summary>Verilen konaklama için politika nesnesini üretir.</summary>
    /// <param name="hotel">Otel bağlamı (politika + saat dilimi).</param>
    /// <param name="checkIn">Giriş günü.</param>
    /// <param name="accommodationGross">Ücret matrahı — <b>Kurtaxe hariç</b> konaklama tutarı.</param>
    /// <param name="now">Değerlendirme anı (UTC).</param>
    public static PublicCancellationPolicyResponse Build(
        PublicHotelContext hotel,
        DateOnly checkIn,
        decimal accommodationGross,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        var policy = hotel.Hotel.CancellationPolicy;
        var deadline = FreeCancellationDeadline(hotel, checkIn);

        // Restricted politikada ücretsiz pencere hiç yoktur; son tarih yine gösterilir ki
        // misafir "neden ücret doğuyor" sorusunun cevabını ekranda görebilsin.
        var isFree = policy.Type is CancellationPolicyType.Flexible && now <= deadline;

        return new PublicCancellationPolicyResponse
        {
            Type = policy.Type.ToString(),
            FreeCancellationUntil = hotel.ToHotelLocal(deadline),
            IsFreeCancellationAvailable = isFree,
            LateCancellationFeePercent = policy.LateCancellationFeePercent,
            LateCancellationFeeAmount = Fee(accommodationGross, policy.LateCancellationFeePercent),
            NoShowFeePercent = policy.NoShowFeePercent,
            NoShowFeeAmount = Fee(accommodationGross, policy.NoShowFeePercent),
            CityTaxRefundedOnCancellation = true,
            PolicyTextKey = policy.Type is CancellationPolicyType.Flexible
                ? "legal.cancellation.flexible"
                : "legal.cancellation.restricted"
        };
    }

    /// <summary>Ücretsiz iptalin son anı (mutlak, UTC tabanlı karşılaştırma için).</summary>
    public static DateTimeOffset FreeCancellationDeadline(PublicHotelContext hotel, DateOnly checkIn)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        var policy = hotel.Hotel.CancellationPolicy;
        var deadlineDate = checkIn.AddDays(-policy.FreeCancellationDaysBeforeArrival);

        return PublicTimeZone.ToInstant(deadlineDate, policy.CutoffLocalTime, hotel.TimeZone);
    }

    /// <summary>
    /// Şu anda iptal edilirse doğacak ücret. Ücretsiz pencerede <c>0,00</c>; dışında
    /// <c>accommodationGross × lateCancellationFeePercent</c>.
    /// </summary>
    public static decimal FeeIfCancelledNow(
        PublicHotelContext hotel,
        DateOnly checkIn,
        decimal accommodationGross,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hotel);

        var policy = hotel.Hotel.CancellationPolicy;
        var isFree = policy.Type is CancellationPolicyType.Flexible
                     && now <= FreeCancellationDeadline(hotel, checkIn);

        return isFree ? 0m : Fee(accommodationGross, policy.LateCancellationFeePercent);
    }

    /// <summary>Yuvarlama faturanın kullandığı ticari yuvarlamayla aynıdır (yarım yukarı).</summary>
    private static decimal Fee(decimal accommodationGross, decimal percent) =>
        InvoiceAmounts.Round(accommodationGross * percent / 100m);
}
