using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Shifts.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Shifts.Update;

/// <summary>
/// <c>PUT /api/v1/shifts/{id}</c> gövdesi. Çalışan ve gün de değiştirilebilir (plan ızgarasında
/// sürükle-bırak); yeni <c>(employeeId, date)</c> ikilisi doluysa 409.
/// </summary>
public sealed record UpdateShiftRequest : IRequest<ShiftResponse>, IShiftWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public Guid EmployeeId { get; init; }

    public DateOnly Date { get; init; }

    public ShiftType ShiftType { get; init; } = ShiftType.Morning;

    public string? Note { get; init; }
}
