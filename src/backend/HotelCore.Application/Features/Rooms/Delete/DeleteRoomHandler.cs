using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Rooms.Delete;

/// <summary>
/// Odayı soft-delete eder.
/// <para>
/// Reddedilme koşulu (409): odanın <b>gelecek tarihli</b> (<c>CheckOut &gt;= bugün</c>) ve
/// <b>iptal edilmemiş</b> bir rezervasyonu varsa. Aksi hâlde misafiri olan bir oda
/// listelerden kaybolur ve check-in/out akışı bozulurdu. Geçmiş rezervasyonlar silmeyi
/// engellemez; kayıtlar soft-delete olduğu için tarihçe korunur.
/// </para>
/// </summary>
internal sealed class DeleteRoomHandler(IAppDbContext database, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeleteRoomRequest, Unit>
{
    public async Task<Unit> Handle(DeleteRoomRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await database.Rooms
            .FirstOrDefaultAsync(room => room.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), request.Id);

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        var hasUpcomingReservations = await database.Reservations
            .AnyAsync(
                reservation => reservation.RoomId == entity.Id
                               && reservation.CheckOut >= today
                               && reservation.Status != ReservationStatus.Cancelled,
                cancellationToken)
            .ConfigureAwait(false);

        if (hasUpcomingReservations)
        {
            throw new ConflictException(
                "Bu odanin gelecek tarihli rezervasyonu var; once rezervasyonlari iptal edin veya baska odaya tasiyin.");
        }

        database.Rooms.Remove(entity);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
