using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Reservations.Create;

/// <summary>
/// Rezervasyon sihirbazı — yeni rezervasyon oluşturur. Sıra bilinçlidir:
/// <list type="number">
///   <item>aktif otel (konsolide modda 400),</item>
///   <item>oda ve misafir aktif otelde mi (404),</item>
///   <item>kapasite (400),</item>
///   <item><b>müsaitlik</b>: servis dışı oda veya çakışan tarih (409),</item>
///   <item><b>tutar sunucuda</b> hesaplanır (istemciden gelen tutara güvenilmez),</item>
///   <item>rezervasyon numarası üretilir, folio (açık hesap) açılır.</item>
/// </list>
/// Rezervasyon, folio ve konaklama satırı <b>tek</b> <c>SaveChanges</c> ile yazılır: yarıda
/// kalmış (folio'suz) rezervasyon oluşamaz.
/// </summary>
internal sealed class CreateReservationHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    IAvailabilityService availability,
    ReservationReader reader,
    ReservationPricingService pricing,
    ReservationNumberGenerator numbers,
    ReservationFolioService folios)
    : IRequestHandler<CreateReservationRequest, ReservationResponse>
{
    public async Task<ReservationResponse> Handle(
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotelId = currentUser.RequireHotelId();

        var room = await reader.GetRoomForBookingAsync(request.RoomId, hotelId, cancellationToken)
            .ConfigureAwait(false);
        ReservationReader.EnsureCapacity(room, request.Adults, request.Children);

        await reader.EnsureGuestExistsAsync(request.GuestId, hotelId, cancellationToken)
            .ConfigureAwait(false);

        await availability.EnsureRoomIsBookableAsync(
                request.RoomId,
                request.CheckIn,
                request.CheckOut,
                excludeReservationId: null,
                cancellationToken)
            .ConfigureAwait(false);

        var price = await pricing.CalculateAsync(
                request.RoomId,
                request.CheckIn,
                request.CheckOut,
                request.Channel,
                cancellationToken)
            .ConfigureAwait(false);

        var reservation = new Reservation
        {
            HotelId = hotelId,
            RoomId = request.RoomId,
            GuestId = request.GuestId,
            RatePlanId = price.RatePlanId,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            Adults = request.Adults,
            Children = request.Children,
            Status = request.Status ?? ReservationStatus.Option,
            Channel = request.Channel,
            TotalAmount = price.TotalAmount,
            DepositPercent = request.DepositPercent,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        database.Reservations.Add(reservation);

        // Folio konaklamanin basinda acilir (Odoo akisi): masraflar burada birikir, check-out'ta
        // faturaya donusur. Kimlik uygulama tarafinda uretildigi icin ayni SaveChanges'te yazilir.
        await folios.SyncRoomChargeAsync(reservation, price.Nights, cancellationToken)
            .ConfigureAwait(false);

        await SaveWithUniqueNumberAsync(reservation, hotelId, cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(reservation.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rezervasyon numarasını atar ve kaydeder. Eşzamanlı iki istek aynı numarayı üretirse
    /// <c>(HotelId, ReservationNumber)</c> unique index'i devreye girer; <c>AppDbContext</c> bunu
    /// <see cref="ConflictException"/>'a çevirdiği için burada numara yenilenip yeniden denenir.
    /// Böylece kullanıcı yarış durumunda hata almaz (fatura numarasındaki satır kilidine gerek
    /// kalmadan — rezervasyon numarasında boşluk olması sorun değildir).
    /// </summary>
    private async Task SaveWithUniqueNumberAsync(
        Reservation reservation,
        Guid hotelId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            reservation.ReservationNumber = await numbers.NextAsync(hotelId, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (ConflictException) when (attempt < ReservationNumberGenerator.MaxAttempts)
            {
                // Numara cakismasi: sonraki turda yeni numara uretilip tekrar denenir.
            }
        }
    }
}
