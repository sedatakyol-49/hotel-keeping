using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Departments.Delete;

/// <summary>
/// <c>DELETE /api/v1/departments/{id}</c>. Departman <b>gerçekten silinir</b>
/// (soft-delete edilemez bir sınıflandırmadır); bağlı çalışan varsa 409.
/// </summary>
public sealed record DeleteDepartmentRequest(Guid Id) : IRequest<Unit>;
