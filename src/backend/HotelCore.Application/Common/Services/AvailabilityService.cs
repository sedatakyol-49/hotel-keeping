using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Common.Services;

/// <summary>
/// <see cref="IAvailabilityService"/> implementasyonu — <b>Application katmanında</b>: müsaitlik
/// bir iş kuralıdır, veri erişimi ise <see cref="IAppDbContext"/> portu üzerinden yapılır
/// (Infrastructure'a sızmaz).
/// <para>
/// Sorgular <c>Reservation(HotelId, RoomId, CheckIn, CheckOut)</c> index'ini kullanır; tenant
/// izolasyonu ve soft-delete global query filter'dan gelir, burada elle yazılmaz.
/// </para>
/// </summary>
internal sealed class AvailabilityService(IAppDbContext database) : IAvailabilityService
{
    public async Task<bool> IsRoomFreeAsync(
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? excludeReservationId,
        CancellationToken cancellationToken)
    {
        var hasOverlap = await OverlappingQuery(roomId, checkIn, checkOut, excludeReservationId)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        return !hasOverlap;
    }

    public async Task EnsureRoomIsBookableAsync(
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? excludeReservationId,
        CancellationToken cancellationToken)
    {
        // Oda kapsam kontrolü: başka otelin odası global filter yüzünden görünmez -> 404.
        var room = await database.Rooms
            .Where(candidate => candidate.Id == roomId)
            .Select(candidate => new { candidate.Id, candidate.Number, candidate.IsOutOfOrder })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), roomId);

        if (room.IsOutOfOrder)
        {
            throw new ConflictException(Messages.RoomOutOfOrder(room.Number));
        }

        var conflict = await OverlappingQuery(roomId, checkIn, checkOut, excludeReservationId)
            .Select(reservation => new
            {
                reservation.ReservationNumber,
                reservation.CheckIn,
                reservation.CheckOut
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (conflict is not null)
        {
            throw new ConflictException(Messages.RoomNotAvailable(
                room.Number,
                checkIn,
                checkOut,
                conflict.ReservationNumber,
                conflict.CheckIn,
                conflict.CheckOut));
        }
    }

    public async Task<IReadOnlyList<Guid>> GetAvailableRoomIdsAsync(
        DateOnly rangeStart,
        DateOnly rangeEnd,
        Guid? roomTypeId,
        CancellationToken cancellationToken)
    {
        var rooms = database.Rooms.Where(room => !room.IsOutOfOrder);

        if (roomTypeId is Guid typeId)
        {
            rooms = rooms.Where(room => room.RoomTypeId == typeId);
        }

        // "Aralık boyunca tamamı müsait" = aralıkla kesişen bloke edici rezervasyonu OLMAYAN oda.
        // Tek sorgu + NOT EXISTS: oda başına ayrı çakışma sorgusu (N+1) yapılmaz.
        var blocked = database.Reservations.BlockingBetween(rangeStart, rangeEnd);

        return await rooms
            .Where(room => !blocked.Any(reservation => reservation.RoomId == room.Id))
            .Select(room => room.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Aynı odada, verilen yarı açık aralıkla kesişen bloke edici rezervasyonlar.
    /// <paramref name="excludeReservationId"/> güncellenen kaydın kendisiyle çakışmasını önler.
    /// </summary>
    private IQueryable<Reservation> OverlappingQuery(
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? excludeReservationId) =>
        database.Reservations
            .Where(reservation => reservation.RoomId == roomId
                                  && (excludeReservationId == null || reservation.Id != excludeReservationId))
            .BlockingBetween(checkIn, checkOut);
}
