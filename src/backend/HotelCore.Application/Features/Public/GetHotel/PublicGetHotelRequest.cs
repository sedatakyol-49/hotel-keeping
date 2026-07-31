using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetHotel;

/// <summary>
/// <c>GET /api/v1/public/hotels/{hotelSlug}</c> — otel künyesi ve politikalar.
/// <para>
/// <b>Slug istekte taşınmaz:</b> aktif otel <c>PublicTenantMiddleware</c> tarafından yoldan
/// çözülüp tenant kapsamına kurulmuştur; handler onu <c>ITenantContext</c>'ten okur. Böylece
/// "yolda A oteli, gövdede B oteli" gibi bir tutarsızlık hiç oluşamaz.
/// </para>
/// </summary>
public sealed record PublicGetHotelRequest : IRequest<PublicHotelResponse>;
