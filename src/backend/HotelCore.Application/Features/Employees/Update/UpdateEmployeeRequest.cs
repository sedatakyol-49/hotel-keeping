using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Employees.Update;

/// <summary><c>PUT /api/v1/employees/{id}</c> gövdesi.</summary>
public sealed record UpdateEmployeeRequest : IRequest<EmployeeResponse>, IEmployeeWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? StaffNumber { get; init; }

    public Guid DepartmentId { get; init; }

    public EmploymentType EmploymentType { get; init; } = EmploymentType.FullTime;

    public decimal AnnualLeaveDays { get; init; }

    public DateOnly HiredOn { get; init; }

    public DateOnly? TerminatedOn { get; init; }
}
