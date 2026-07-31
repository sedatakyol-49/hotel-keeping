using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.CancelBooking;

/// <summary>
/// <c>POST /api/v1/public/hotels/{hotelSlug}/bookings/{accessToken}/cancel</c>.
/// <para>
/// <b><see cref="AcknowledgedFeeAmount"/> neden var:</b> misafirin ücreti <i>görmeden</i> iptal
/// etmesini engellemek. Ücret doğacaksa tutar teyidi zorunludur ve sunucunun hesabıyla
/// eşleşmelidir; eşleşmezse <c>409 FEE_ACKNOWLEDGEMENT_REQUIRED</c> ve yanıt <b>doğru tutarı</b>
/// bildirir.
/// </para>
/// </summary>
public sealed record PublicCancelBookingRequest : IRequest<PublicBookingResponse>
{
    /// <summary>Route'tan doldurulur; gövdeden okunmaz.</summary>
    [JsonIgnore]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Opsiyonel iptal gerekçesi (≤ 500) — rezervasyon notlarına <b>damgalı eklenir</b>.</summary>
    public string? Reason { get; init; }

    /// <summary>Ücret doğacaksa zorunlu; ücretsizse gönderilmemelidir (veya <c>0.00</c>).</summary>
    public decimal? AcknowledgedFeeAmount { get; init; }
}
