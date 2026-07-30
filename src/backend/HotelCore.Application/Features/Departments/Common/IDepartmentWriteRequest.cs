namespace HotelCore.Application.Features.Departments.Common;

/// <summary>Create ve Update isteklerinin paylaştığı gövde sözleşmesi.</summary>
public interface IDepartmentWriteRequest
{
    string Name { get; }

    string? Description { get; }
}
