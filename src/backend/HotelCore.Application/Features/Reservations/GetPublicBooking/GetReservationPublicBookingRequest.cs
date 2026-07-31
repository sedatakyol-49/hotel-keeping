using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Reservations.GetPublicBooking;

/// <summary>
/// <c>GET /api/v1/reservations/{id}/public-booking</c> — rıza ve hukuki anlık görüntü.
/// İzin: <c>Reservations.View</c> (<b>yeni izin anahtarı yoktur</b>).
/// </summary>
/// <param name="ReservationId">Rezervasyon kimliği.</param>
public sealed record GetReservationPublicBookingRequest(Guid ReservationId)
    : IRequest<ReservationPublicBookingResponse>;
