using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.NoShow;

/// <summary>
/// Rezervasyonu "gelmedi" olarak işaretler (<c>Option</c>/<c>Confirmed</c> → <c>NoShow</c>).
/// Geçiş kuralı <see cref="ReservationStatusMachine"/>'dan gelir; check-in yapmış misafir
/// no-show olamaz → 409.
/// </summary>
internal sealed class MarkNoShowHandler(IAppDbContext database, ReservationReader reader)
    : IRequestHandler<MarkNoShowRequest, ReservationResponse>
{
    public async Task<ReservationResponse> Handle(
        MarkNoShowRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        ReservationStatusMachine.EnsureCanTransition(reservation.Status, ReservationStatus.NoShow);

        reservation.Status = ReservationStatus.NoShow;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(reservation.Id, cancellationToken).ConfigureAwait(false);
    }
}
