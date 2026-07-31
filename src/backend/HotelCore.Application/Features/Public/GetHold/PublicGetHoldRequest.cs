using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetHold;

/// <summary>
/// <c>GET /api/v1/public/hotels/{hotelSlug}/holds/{holdToken}</c> — kalan süre ve <b>donmuş</b>
/// teklif. Yeni bir teklif hesaplanmaz: sayfa yenilendiğinde misafirin gördüğü fiyat
/// değişmemelidir (§312j Abs. 2).
/// </summary>
/// <param name="HoldToken">Ham hold token'ı; veritabanında yalnızca SHA-256 özeti vardır.</param>
public sealed record PublicGetHoldRequest(string HoldToken) : IRequest<PublicHoldResponse>;
