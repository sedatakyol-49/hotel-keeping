using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Employees.Common;

/// <summary>Create ve Update isteklerinin paylaştığı gövde sözleşmesi.</summary>
public interface IEmployeeWriteRequest
{
    string FirstName { get; }

    string LastName { get; }

    string? Email { get; }

    string? Phone { get; }

    string? StaffNumber { get; }

    Guid DepartmentId { get; }

    EmploymentType EmploymentType { get; }

    decimal AnnualLeaveDays { get; }

    DateOnly HiredOn { get; }

    DateOnly? TerminatedOn { get; }
}
