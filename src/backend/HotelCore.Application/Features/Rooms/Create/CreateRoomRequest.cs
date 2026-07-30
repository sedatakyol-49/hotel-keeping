using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Rooms.Create;

/// <summary>
/// <c>POST /api/v1/rooms</c> gövdesi. <c>housekeepingStatus</c> gönderilmezse yeni oda
/// <see cref="Domain.Enums.HousekeepingStatus.Clean"/> kabul edilir.
/// </summary>
public sealed record CreateRoomRequest : IRequest<RoomResponse>, IRoomWriteRequest
{
    /// <summary>Otel içinde benzersiz oda numarası (çakışma → 409).</summary>
    public string Number { get; init; } = string.Empty;

    public int Floor { get; init; }

    /// <summary>Aynı otele ait oda tipi olmalıdır; aksi hâlde 404.</summary>
    public Guid RoomTypeId { get; init; }

    /// <summary>Opsiyonel başlangıç durumu (varsayılan <c>Clean</c>).</summary>
    public HousekeepingStatus? HousekeepingStatus { get; init; }

    /// <summary>
    /// Opsiyonel servis dışı bayrağı. Durumla tutarlılığı otomatik korunur
    /// (bkz. <c>HousekeepingState</c>).
    /// </summary>
    public bool? IsOutOfOrder { get; init; }

    public string? Note { get; init; }
}
