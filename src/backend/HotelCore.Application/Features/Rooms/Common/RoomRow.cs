using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Rooms.Common;

/// <summary>
/// Oda sorgusunun düz izdüşümü (yalnızca gereken kolonlar; navigasyon materyalize edilmez).
/// </summary>
/// <param name="Id">Oda kimliği.</param>
/// <param name="Number">Oda numarası.</param>
/// <param name="Floor">Kat.</param>
/// <param name="RoomTypeId">Oda tipi kimliği.</param>
/// <param name="RoomTypeCode">Oda tipi kodu.</param>
/// <param name="RoomTypeName">Oda tipinin varsayılan dildeki adı (çeviri fallback'i).</param>
/// <param name="HousekeepingStatus">Kat hizmetleri durumu (enum).</param>
/// <param name="IsOutOfOrder">Servis dışı bayrağı.</param>
/// <param name="Note">Kat hizmetleri notu.</param>
internal sealed record RoomRow(
    Guid Id,
    string Number,
    int Floor,
    Guid RoomTypeId,
    string RoomTypeCode,
    string RoomTypeName,
    HousekeepingStatus HousekeepingStatus,
    bool IsOutOfOrder,
    string? Note);

/// <summary>
/// Pano sorgusunun izdüşümü. <b>Bilinçli olarak</b> hiçbir fiyat/para alanı içermez
/// (bkz. <see cref="RoomBoardItemDto"/> — RBAC §7); oda tipi adı da gerekmediği için çekilmez.
/// </summary>
/// <param name="Id">Oda kimliği.</param>
/// <param name="Number">Oda numarası.</param>
/// <param name="Floor">Kat.</param>
/// <param name="RoomTypeCode">Oda tipi kodu.</param>
/// <param name="HousekeepingStatus">Kat hizmetleri durumu (enum).</param>
/// <param name="IsOutOfOrder">Servis dışı bayrağı.</param>
/// <param name="Note">Kat hizmetleri notu.</param>
internal sealed record RoomBoardRow(
    Guid Id,
    string Number,
    int Floor,
    string RoomTypeCode,
    HousekeepingStatus HousekeepingStatus,
    bool IsOutOfOrder,
    string? Note);
