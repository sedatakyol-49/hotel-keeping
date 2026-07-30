using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.GetById;

/// <summary><c>GET /api/v1/vacations/{id}</c> — başka otelin kaydı 404 döner.</summary>
public sealed record GetVacationByIdRequest(Guid Id) : IRequest<VacationRequestResponse>;
