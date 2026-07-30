using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Departments.Common;

namespace HotelCore.Application.Features.Departments.List;

/// <summary><c>GET /api/v1/departments</c> — aktif otelin departmanları (sayfalama yoktur).</summary>
public sealed record ListDepartmentsRequest : IRequest<IReadOnlyList<DepartmentResponse>>;
