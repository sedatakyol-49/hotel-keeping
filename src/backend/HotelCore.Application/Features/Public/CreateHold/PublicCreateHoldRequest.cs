using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.CreateHold;

/// <summary>
/// <c>POST /api/v1/public/hotels/{hotelSlug}/holds</c> — teklifi <b>15 dakika</b> dondurur.
/// <para>
/// <b>Kişisel veri taşımaz:</b> ad, e-posta, telefon bu adımda <b>sorulmaz</b> (DSGVO Art. 5
/// Abs. 1 lit. c). Misafir henüz hiçbir şey beyan etmemiştir; terk edilmiş bir sepetin kişisel
/// veri bırakması veri minimizasyonuna aykırı olurdu.
/// </para>
/// </summary>
public sealed record PublicCreateHoldRequest : IRequest<PublicHoldResponse>
{
    public string RoomTypeCode { get; init; } = string.Empty;

    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    public int Adults { get; init; } = 1;

    public int Children { get; init; }

    /// <summary>
    /// İstemci IP'si — <b>controller doldurur, gövdeden okunmaz</b> ve yanıta yazılmaz.
    /// Yalnızca tuzlanmış özeti saklanır (kötüye kullanım analizi).
    /// </summary>
    [JsonIgnore]
    public string? ClientIp { get; init; }
}
