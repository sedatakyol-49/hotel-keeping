using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Public.LookupBooking;

/// <summary>
/// <c>POST /api/v1/public/hotels/{hotelSlug}/bookings/lookup</c> — bağlantısını kaybeden misafir
/// için.
///
/// <para><b>Hiçbir koşulda veri döndürmez.</b> Yanıt her zaman <c>202</c> ve gövdesizdir:
/// <list type="bullet">
///   <item>eşleşme varsa erişim bağlantısı e-postayla gönderilir,</item>
///   <item>eşleşme yoksa hiçbir şey yapılmaz,</item>
///   <item>geçersiz biçimli referans da <c>202</c> alır.</item>
/// </list>
/// Ayrıca <b>sabit bir minimum işlem süresi</b> uygulanır: ne yanıt gövdesi ne yanıt <i>süresi</i>
/// bir rezervasyonun varlığını sızdırır.</para>
///
/// <para><b>Neden <c>bookingReference</c> tek başına yetmiyor:</b> referans bir taşıyıcı kimlik
/// bilgisi değildir (60 bit, telefonda söylenir, e-postada görünür). Tek başına veri döndürseydi
/// hız sınırına rağmen bir numaralandırma yüzeyi olurdu; bu yüzden yanıt <b>her zaman</b>
/// e-postaya gider.</para>
/// </summary>
public sealed record PublicLookupBookingRequest : IRequest<Unit>
{
    public string BookingReference { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    /// <summary>Controller doldurur; gövdeden okunmaz.</summary>
    [JsonIgnore]
    public string? ClientIp { get; init; }
}
