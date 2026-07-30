using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Shifts.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Shifts.Create;

/// <summary><c>POST /api/v1/shifts</c> gövdesi.</summary>
public sealed record CreateShiftRequest : IRequest<ShiftResponse>, IShiftWriteRequest
{
    /// <summary>Aynı otele ait çalışan olmalıdır; aksi hâlde 404.</summary>
    public Guid EmployeeId { get; init; }

    /// <summary>Vardiya günü — çalışan başına günde tek vardiya (çakışma → 409).</summary>
    public DateOnly Date { get; init; }

    public ShiftType ShiftType { get; init; } = ShiftType.Morning;

    public string? Note { get; init; }
}
