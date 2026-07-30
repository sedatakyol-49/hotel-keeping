using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Employees.Delete;

/// <summary>
/// <c>DELETE /api/v1/employees/{id}</c> — soft-delete. Çalışanın izin/zaman kayıtları
/// korunur; kayıt yalnızca listelerden düşer.
/// </summary>
public sealed record DeleteEmployeeRequest(Guid Id) : IRequest<Unit>;
