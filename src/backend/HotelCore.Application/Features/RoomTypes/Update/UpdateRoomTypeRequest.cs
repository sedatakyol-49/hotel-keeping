using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.Update;

/// <summary>
/// <c>PUT /api/v1/room-types/{id}</c> gövdesi.
/// </summary>
public sealed record UpdateRoomTypeRequest : IRequest<RoomTypeResponse>, IRoomTypeWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ (Auth slice'ındaki desen).</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public decimal BasePrice { get; init; }

    public int Capacity { get; init; }

    public int? SizeSqm { get; init; }

    public IReadOnlyList<string>? Amenities { get; init; }

    /// <summary>
    /// Opsiyonel çeviriler (upsert). Gönderilen dil güncellenir/eklenir; değeri <c>null</c>
    /// gönderilen dil silinir; hiç gönderilmeyen dil olduğu gibi kalır.
    /// </summary>
    public IReadOnlyDictionary<string, RoomTypeTranslationDto?>? Translations { get; init; }
}
