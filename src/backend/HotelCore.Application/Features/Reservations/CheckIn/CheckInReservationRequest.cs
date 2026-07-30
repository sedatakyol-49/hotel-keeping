using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.CheckIn;

/// <summary>
/// <c>POST /api/v1/reservations/{id}/check-in</c> — gövdesizdir.
/// <para>
/// Yalnızca <c>Option</c> / <c>Confirmed</c> durumundan yapılabilir (aksi hâlde 409);
/// <b>giriş tarihinden önce</b> check-in denemesi 409; oda servis dışıysa 409.
/// </para>
/// </summary>
public sealed record CheckInReservationRequest(Guid Id) : IRequest<ReservationResponse>;
