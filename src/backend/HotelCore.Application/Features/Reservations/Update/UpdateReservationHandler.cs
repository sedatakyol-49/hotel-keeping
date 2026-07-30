using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.Update;

/// <summary>
/// Rezervasyonu günceller: <b>müsaitlik yeniden doğrulanır</b> (kendisi hariç tutulur, aksi
/// hâlde rezervasyon kendisiyle çakışırdı) ve <b>tutar yeniden hesaplanır</b> — tarih, oda veya
/// kanal değiştiğinde eski tutarın kalması yanlış fiyatlandırma olurdu.
/// <para>
/// Nihai durumdaki (<c>CheckedOut</c> / <c>Cancelled</c> / <c>NoShow</c>) kayıt değiştirilemez → 409
/// (bkz. <see cref="ReservationStatusMachine.EnsureModifiable"/>).
/// </para>
/// </summary>
internal sealed class UpdateReservationHandler(
    IAppDbContext database,
    IAvailabilityService availability,
    ReservationReader reader,
    ReservationPricingService pricing,
    ReservationFolioService folios)
    : IRequestHandler<UpdateReservationRequest, ReservationResponse>
{
    public async Task<ReservationResponse> Handle(
        UpdateReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        ReservationStatusMachine.EnsureModifiable(reservation.Status);

        var room = await reader
            .GetRoomForBookingAsync(request.RoomId, reservation.HotelId, cancellationToken)
            .ConfigureAwait(false);
        ReservationReader.EnsureCapacity(room, request.Adults, request.Children);

        await reader.EnsureGuestExistsAsync(request.GuestId, reservation.HotelId, cancellationToken)
            .ConfigureAwait(false);

        await availability.EnsureRoomIsBookableAsync(
                request.RoomId,
                request.CheckIn,
                request.CheckOut,
                excludeReservationId: reservation.Id,
                cancellationToken)
            .ConfigureAwait(false);

        var price = await pricing.CalculateAsync(
                request.RoomId,
                request.CheckIn,
                request.CheckOut,
                request.Channel,
                cancellationToken)
            .ConfigureAwait(false);

        reservation.RoomId = request.RoomId;
        reservation.GuestId = request.GuestId;
        reservation.CheckIn = request.CheckIn;
        reservation.CheckOut = request.CheckOut;
        reservation.Adults = request.Adults;
        reservation.Children = request.Children;
        reservation.Channel = request.Channel;
        reservation.DepositPercent = request.DepositPercent;
        reservation.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        reservation.TotalAmount = price.TotalAmount;
        reservation.RatePlanId = price.RatePlanId;

        // Folio konaklama satiri yeni tutarla eslesir (fatura henuz olusmadigi icin satir
        // serbestce guncellenebilir; GoBD guard'i yalnizca faturaya bagli satirlara uygular).
        await folios.SyncRoomChargeAsync(reservation, price.Nights, cancellationToken)
            .ConfigureAwait(false);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(reservation.Id, cancellationToken).ConfigureAwait(false);
    }
}
