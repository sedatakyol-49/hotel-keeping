namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// Oda tipi sorgusunun düz (flat) izdüşümü. Entity'nin tamamı değil yalnızca gereken kolonlar
/// çekilir; <see cref="Currency"/> otel satırından, <see cref="RoomCount"/> ise ilişkili oda
/// sayısından SQL tarafında hesaplanır (bellekte döngü/N+1 yok).
/// </summary>
/// <param name="Id">Oda tipi kimliği.</param>
/// <param name="Code">Otel içinde benzersiz kod.</param>
/// <param name="Name">Varsayılan dildeki ad (çeviri yoksa fallback).</param>
/// <param name="Description">Varsayılan dildeki açıklama.</param>
/// <param name="BasePrice">Liste fiyatı.</param>
/// <param name="Currency">Otelin para birimi.</param>
/// <param name="Capacity">Maksimum kişi sayısı.</param>
/// <param name="SizeSqm">Oda büyüklüğü (m²).</param>
/// <param name="Amenities">Virgülle ayrılmış donanım metni (DB biçimi).</param>
/// <param name="RoomCount">Bağlı silinmemiş oda sayısı.</param>
internal sealed record RoomTypeRow(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal BasePrice,
    string Currency,
    int Capacity,
    int? SizeSqm,
    string? Amenities,
    int RoomCount);
