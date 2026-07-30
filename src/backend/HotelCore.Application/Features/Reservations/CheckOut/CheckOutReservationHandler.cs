using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Reservations.CheckOut;

/// <summary>
/// Misafiri çıkarır: <c>CheckedIn</c> → <c>CheckedOut</c> (başka bir durumdan yapılamaz → 409).
/// <para>
/// <b>Housekeeping tetiklemesi (architecture.md §5):</b> çıkış yapılan odanın
/// <c>HousekeepingStatus</c>'u <b>otomatik</b> <c>Dirty</c> olur — Odoo housekeeping akışı.
/// Durum değişikliği rezervasyonla <b>aynı <c>SaveChanges</c></b> içinde yazılır: ya ikisi
/// birlikte olur ya hiçbiri (yarım kalmış "çıkış yapıldı ama oda temiz görünüyor" durumu oluşamaz).
/// </para>
/// <para>
/// Servis dışı bir oda <c>Dirty</c>'ye çekilmez: <see cref="HousekeepingState"/> değişmezi
/// <c>isOutOfOrder ↔ OutOfOrder</c> tutarlılığını korur (arıza kaydı check-out ile silinmez).
/// </para>
/// <para>
/// Folio <b>kapatılmaz</b>: fatura henüz yok, açık hesap durur. Kapatma faturalama modülünün
/// (Invoice) işidir.
/// </para>
/// </summary>
internal sealed class CheckOutReservationHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    ReservationReader reader)
    : IRequestHandler<CheckOutReservationRequest, ReservationResponse>
{
    public async Task<ReservationResponse> Handle(
        CheckOutReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        ReservationStatusMachine.EnsureCanTransition(reservation.Status, ReservationStatus.CheckedOut);

        var room = await database.Rooms
            .FirstOrDefaultAsync(candidate => candidate.Id == reservation.RoomId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), reservation.RoomId);

        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = clock.UtcNow;

        // Oda kirli olarak isaretlenir (Odoo housekeeping akisi). Servis disi odada durum
        // korunur; tutarlilik kurali oda modulundeki tek noktadan uygulanir.
        HousekeepingState.Apply(room, HousekeepingStatus.Dirty, room.IsOutOfOrder);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(reservation.Id, cancellationToken).ConfigureAwait(false);
    }
}
