using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.RatePlans.Create;

/// <summary><c>POST /api/v1/rate-plans</c> gövdesi.</summary>
public sealed record CreateRatePlanRequest : IRequest<RatePlanResponse>, IRatePlanWriteRequest
{
    /// <summary>Aynı otele ait oda tipi olmalıdır; aksi hâlde 404.</summary>
    public Guid RoomTypeId { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateOnly ValidTo { get; init; }

    /// <summary>Verilmezse plan tüm kanallar için geçerlidir.</summary>
    public ReservationChannel? Channel { get; init; }

    /// <summary>Opsiyonel; varsayılan <c>true</c>.</summary>
    public bool? IsActive { get; init; }
}
