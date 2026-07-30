using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Departments.Common;

namespace HotelCore.Application.Features.Departments.Update;

/// <summary><c>PUT /api/v1/departments/{id}</c> gövdesi.</summary>
public sealed record UpdateDepartmentRequest
    : IRequest<DepartmentResponse>, IDepartmentWriteRequest
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
}
