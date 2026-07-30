namespace HotelCore.Application.Features.Shifts.Common;

/// <summary>
/// Tek vardiya kaydı — api-contracts.md → "HR (Vacation / TimeTracking / Shifts)".
/// </summary>
public sealed record ShiftResponse
{
    public Guid Id { get; init; }

    public Guid EmployeeId { get; init; }

    public string EmployeeName { get; init; } = string.Empty;

    /// <summary>Vardiya günü (takvim günü).</summary>
    public DateOnly Date { get; init; }

    /// <summary>Vardiya tipi enum <b>adı</b> (string): <c>Morning | Evening | Night | Off</c>.</summary>
    public string ShiftType { get; init; } = string.Empty;

    public string? Note { get; init; }
}
