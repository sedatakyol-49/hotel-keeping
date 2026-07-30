using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.RatePlans.Common;

/// <summary>
/// Fiyat planı sorgusunun düz izdüşümü. Nullable enum (<c>Channel</c>) ham okunur; string'e
/// çevirme C# tarafında yapılır (sağlayıcıdan bağımsız, deterministik).
/// </summary>
/// <param name="Id">Plan kimliği.</param>
/// <param name="RoomTypeId">Oda tipi kimliği.</param>
/// <param name="RoomTypeCode">Oda tipi kodu.</param>
/// <param name="RoomTypeName">Oda tipi adı (varsayılan dil).</param>
/// <param name="Name">Plan adı.</param>
/// <param name="Price">Gecelik fiyat.</param>
/// <param name="Currency">Otelin para birimi.</param>
/// <param name="ValidFrom">Geçerlilik başlangıcı (dahil).</param>
/// <param name="ValidTo">Geçerlilik bitişi (dahil).</param>
/// <param name="Channel">Kanal (null = tüm kanallar).</param>
/// <param name="IsActive">Aktiflik.</param>
internal sealed record RatePlanRow(
    Guid Id,
    Guid RoomTypeId,
    string RoomTypeCode,
    string RoomTypeName,
    string Name,
    decimal Price,
    string Currency,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    ReservationChannel? Channel,
    bool IsActive);
