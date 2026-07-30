using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Cancel;

/// <summary>
/// Rezervasyonu iptal eder (<c>Option</c>/<c>Confirmed</c> → <c>Cancelled</c>).
/// <para>
/// İptal edilen rezervasyon <b>silinmez</b>: kayıt kalır, numarası korunur ve oda takviminden
/// düşer (iptal/no-show çakışma üretmez — bkz. <c>IAvailabilityService</c>). Böylece oda tekrar
/// satılabilir ama tarihçe kaybolmaz.
/// </para>
/// </summary>
internal sealed class CancelReservationHandler(
    IAppDbContext database,
    IDateTimeProvider clock,
    ReservationReader reader)
    : IRequestHandler<CancelReservationRequest, ReservationResponse>
{
    public async Task<ReservationResponse> Handle(
        CancelReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        ReservationStatusMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Cancelled);

        reservation.Status = ReservationStatus.Cancelled;

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            // Gerekce nota eklenir (uzerine yazilmaz): resepsiyonun girdigi notlar korunur.
            var stamp = $"[{clock.UtcNow.UtcDateTime:yyyy-MM-dd} Iptal] {request.Reason.Trim()}";
            var combined = string.IsNullOrWhiteSpace(reservation.Notes)
                ? stamp
                : reservation.Notes + "\n" + stamp;

            // Kolon siniri (1000) asilirsa veritabani hatasi 500 dondururdu; sondan kirpilir.
            reservation.Notes = combined.Length > MaxNotesLength
                ? combined[..MaxNotesLength]
                : combined;
        }

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(reservation.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>Reservation.Notes</c> kolonunun uzunluk sınırı (ReservationConfiguration).</summary>
    private const int MaxNotesLength = 1000;
}
