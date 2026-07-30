using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reservations.CheckIn;

/// <summary>
/// Misafiri otele alır: <c>Option</c>/<c>Confirmed</c> → <c>CheckedIn</c>.
/// <list type="bullet">
///   <item>Geçiş kuralı tek yerden gelir (<see cref="ReservationStatusMachine"/>) → geçersizse 409.</item>
///   <item><b>Erken check-in engellenir:</b> bugün giriş gününden önce ise 409. Aksi hâlde
///         gelecek haftanın misafiri bugün otelde görünür, oda takvimi ve doluluk raporu bozulur.
///         (Geç check-in serbesttir: misafir bir gün sonra gelebilir.)</item>
///   <item>Oda servis dışıysa 409 — arızalı odaya misafir yerleştirilemez.</item>
/// </list>
/// </summary>
internal sealed class CheckInReservationHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    ReservationReader reader)
    : IRequestHandler<CheckInReservationRequest, ReservationResponse>
{
    public async Task<ReservationResponse> Handle(
        CheckInReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        ReservationStatusMachine.EnsureCanTransition(reservation.Status, ReservationStatus.CheckedIn);

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        if (today < reservation.CheckIn)
        {
            throw new ConflictException(Messages.CheckInBeforeArrival(reservation.CheckIn, today));
        }

        var room = await database.Rooms
            .FirstOrDefaultAsync(candidate => candidate.Id == reservation.RoomId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), reservation.RoomId);

        if (room.IsOutOfOrder)
        {
            throw new ConflictException(Messages.CheckInRoomOutOfOrder(room.Number));
        }

        reservation.Status = ReservationStatus.CheckedIn;
        reservation.CheckedInAt = clock.UtcNow;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(reservation.Id, cancellationToken).ConfigureAwait(false);
    }
}
