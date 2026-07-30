using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Employees.Common;

namespace HotelCore.Application.Features.Employees.GetById;

/// <summary><c>GET /api/v1/employees/{id}</c> — başka otelin kaydı 404 döner.</summary>
public sealed record GetEmployeeByIdRequest(Guid Id) : IRequest<EmployeeResponse>;
