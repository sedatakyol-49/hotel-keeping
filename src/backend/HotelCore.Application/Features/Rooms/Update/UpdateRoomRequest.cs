using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Rooms.Update;

/// <summary>
/// <c>PUT /api/v1/rooms/{id}</c> gövdesi (tam güncelleme). Kat hizmetleri durumunu yalnızca
/// housekeeping ekibine açmak için ayrı bir uç vardır: <c>PATCH /rooms/{id}/housekeeping</c>.
/// </summary>
public sealed record UpdateRoomRequest : IRequest<RoomResponse>, IRoomWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public string Number { get; init; } = string.Empty;

    public int Floor { get; init; }

    public Guid RoomTypeId { get; init; }

    /// <summary>Kat hizmetleri durumu (<c>Clean | Dirty | Inspected | OutOfOrder</c>).</summary>
    public HousekeepingStatus HousekeepingStatus { get; init; }

    /// <summary>Servis dışı bayrağı; durumla tutarlılığı otomatik korunur.</summary>
    public bool IsOutOfOrder { get; init; }

    public string? Note { get; init; }
}
