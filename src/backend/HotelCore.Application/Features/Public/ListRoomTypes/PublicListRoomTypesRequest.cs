using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.ListRoomTypes;

/// <summary>
/// <c>GET /api/v1/public/hotels/{hotelSlug}/room-types</c> — tarihsiz katalog.
/// <para>
/// Katalogda <b>oda sayısı, doluluk ve oda numarası yoktur</b>; fiyat bir <b>"ab" fiyatıdır</b>
/// (<c>RoomType.BasePrice</c>) ve ekranda öyle etiketlenmelidir.
/// </para>
/// </summary>
public sealed record PublicListRoomTypesRequest : IRequest<IReadOnlyList<PublicRoomTypeSummaryResponse>>;
