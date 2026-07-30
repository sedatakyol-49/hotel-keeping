using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Departments.Common;

namespace HotelCore.Application.Features.Departments.Create;

/// <summary><c>POST /api/v1/departments</c> gövdesi. Ad otel içinde benzersizdir (409).</summary>
public sealed record CreateDepartmentRequest
    : IRequest<DepartmentResponse>, IDepartmentWriteRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}
