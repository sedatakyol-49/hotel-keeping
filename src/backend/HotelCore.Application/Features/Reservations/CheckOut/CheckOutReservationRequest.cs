using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.CheckOut;

/// <summary>
/// <c>POST /api/v1/reservations/{id}/check-out</c> — gövdesizdir.
/// <para>
/// Yalnızca <c>CheckedIn</c> durumundan yapılabilir (aksi hâlde 409). Oda kat hizmetleri
/// durumu <b>otomatik</b> <c>Dirty</c>'ye geçer (architecture.md §5).
/// </para>
/// </summary>
public sealed record CheckOutReservationRequest(Guid Id) : IRequest<ReservationResponse>;
