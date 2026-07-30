using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Employees.Create;

/// <summary><c>POST /api/v1/employees</c> gövdesi.</summary>
public sealed record CreateEmployeeRequest : IRequest<EmployeeResponse>, IEmployeeWriteRequest
{
    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary>Verilirse otel içinde benzersiz olmalıdır (çakışma → 409).</summary>
    public string? StaffNumber { get; init; }

    /// <summary>Aynı otele ait departman olmalıdır; aksi hâlde 404.</summary>
    public Guid DepartmentId { get; init; }

    public EmploymentType EmploymentType { get; init; } = EmploymentType.FullTime;

    public decimal AnnualLeaveDays { get; init; }

    public DateOnly HiredOn { get; init; }

    public DateOnly? TerminatedOn { get; init; }
}
