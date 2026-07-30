using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.GetById;

/// <summary><c>GET /api/v1/reservations/{id}</c> — başka otelin kaydı 404 döner.</summary>
public sealed record GetReservationByIdRequest(Guid Id) : IRequest<ReservationResponse>;
