using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;

namespace HotelCore.Application.Features.Public.GetRoomType;

/// <summary>
/// <c>GET /api/v1/public/hotels/{hotelSlug}/room-types/{roomTypeCode}</c> — oda tipi detayı.
/// <para>
/// <c>roomTypeCode</c> <b>büyük/küçük harf duyarsızdır</b> (<c>dbl</c> = <c>DBL</c>): URL'ler
/// küçük harfle yazılır, kod ise otelde büyük harfle tanımlıdır.
/// </para>
/// </summary>
/// <param name="RoomTypeCode">Oda tipi kodu (public anahtar; GUID kullanılmaz).</param>
public sealed record PublicGetRoomTypeRequest(string RoomTypeCode) : IRequest<PublicRoomTypeDetailResponse>;
