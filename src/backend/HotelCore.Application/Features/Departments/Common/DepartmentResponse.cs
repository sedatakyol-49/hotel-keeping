namespace HotelCore.Application.Features.Departments.Common;

/// <summary>Departman — api-contracts.md → "Personel (Employees &amp; Departments)".</summary>
public sealed record DepartmentResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Bu departmana bağlı silinmemiş çalışan sayısı.</summary>
    public int EmployeeCount { get; init; }
}
