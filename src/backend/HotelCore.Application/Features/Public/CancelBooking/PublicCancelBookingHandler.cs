using System.Globalization;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Public.CancelBooking;

/// <summary>
/// Misafirin online iptali.
///
/// <para><b>Ücret matrahı yalnızca konaklama tutarıdır; Kurtaxe girmez</b> — konaklama
/// gerçekleşmediği için şehir vergisi hiç doğmaz (<c>CityTaxLiability</c> ile aynı kural).
/// Bu yüzden matrah <b>donmuş</b> <c>accommodationGross</c>'tur, <c>totalGross</c> değil.</para>
///
/// <para><b>Tahsilat ve faturalama BU UÇTA YAPILMAZ.</b> Public sözleşme yalnızca tutarı
/// <i>bildirir</i> ve kaydeder; ücretin nasıl tahsil edileceği (ve KDV'ye tabi olup olmadığı)
/// otelin mevcut faturalama akışının ve açık bir mali sorunun konusudur.</para>
///
/// <para><b>İdempotent değildir:</b> zaten iptal edilmiş rezervasyon <c>409
/// BOOKING_ALREADY_CANCELLED</c> döner. Çift tıklamayı istemci engeller; sunucunun sessizce
/// "başarılı" demesi, ikinci bir iptal ücreti hesaplanmadığını garanti etmezdi.</para>
/// </summary>
internal sealed class PublicCancelBookingHandler(
    IAppDbContext database,
    PublicHotelReader hotels,
    PublicBookingReader bookings,
    IDateTimeProvider clock)
    : IRequestHandler<PublicCancelBookingRequest, PublicBookingResponse>
{
    /// <summary><c>Reservation.Notes</c> kolon sınırı (ReservationConfiguration).</summary>
    private const int MaxNotesLength = 1000;

    public async Task<PublicBookingResponse> Handle(
        PublicCancelBookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var row = await bookings.RequireByAccessTokenAsync(request.AccessToken, cancellationToken)
            .ConfigureAwait(false);

        var reservation = row.Reservation;
        var now = clock.UtcNow;

        if (reservation.Status is ReservationStatus.Cancelled)
        {
            throw PublicApiException.Conflict(
                PublicErrorCodes.BookingAlreadyCancelled,
                Messages.PublicBookingAlreadyCancelled);
        }

        if (!PublicBookingReader.CanCancelOnline(reservation.Status))
        {
            // Giriş yapılmış / tamamlanmış konaklama: misafir oteli aramalıdır.
            throw PublicApiException.Conflict(
                PublicErrorCodes.CancellationNotAllowed,
                Messages.PublicCancellationNotAllowed);
        }

        var fee = ResolveFee(row, now, out var currency);
        EnsureFeeAcknowledged(fee, currency, request.AcknowledgedFeeAmount);

        // Durum geçişi MEVCUT durum makinesinden geçer: public kanal kendi kurallarını uydurmaz.
        ReservationStatusMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Cancelled);
        reservation.Status = ReservationStatus.Cancelled;
        AppendReason(reservation, request.Reason, now);

        row.Booking.CancelledAt = now;
        row.Booking.CancellationFeeAmount = fee;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return bookings.BuildResponse(context, row, rawAccessToken: null);
    }

    /// <summary>
    /// Şu anda iptal edilirse doğacak ücret. Hesap <b>donmuş politikadan</b> okunur: otel yarın
    /// politikasını değiştirdiğinde misafirin onayladığı taahhüt değişmemelidir.
    /// </summary>
    private static decimal ResolveFee(PublicBookingRow row, DateTimeOffset now, out string currency)
    {
        var policy = PublicJson.Deserialize<PublicCancellationPolicyResponse>(
                         row.Booking.CancellationPolicySnapshotJson)
                     ?? new PublicCancellationPolicyResponse();

        var price = PublicJson.Deserialize<PublicPriceResponse>(row.Booking.PriceSnapshotJson)
                    ?? new PublicPriceResponse();

        currency = price.Currency;

        return now <= policy.FreeCancellationUntil ? 0m : policy.LateCancellationFeeAmount;
    }

    /// <summary>
    /// Ücret mutabakatı. Amaç: misafirin tutarı <b>görmeden</b> iptal etmesini engellemek.
    /// Ücretsiz iptalde tutar gönderilmesi de reddedilir — istemcinin yanlış bir ekran
    /// gösterdiğinin işaretidir.
    /// </summary>
    private static void EnsureFeeAcknowledged(decimal fee, string currency, decimal? acknowledged)
    {
        if (fee <= 0m)
        {
            if (acknowledged is decimal value && value != 0m)
            {
                throw PublicApiException.BadRequest(
                    PublicErrorCodes.ValidationFailed,
                    Messages.PublicFeeNotExpected,
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["AcknowledgedFeeAmount"] = [Messages.PublicFeeNotExpected]
                    });
            }

            return;
        }

        if (acknowledged is decimal confirmed && confirmed == fee)
        {
            return;
        }

        throw PublicApiException.Conflict(
            PublicErrorCodes.FeeAcknowledgementRequired,
            Messages.PublicFeeAcknowledgementRequired(fee, currency),
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["AcknowledgedFeeAmount"] =
                [
                    string.Create(CultureInfo.InvariantCulture, $"{fee:0.00} {currency}")
                ]
            });
    }

    /// <summary>Gerekçe nota <b>eklenir</b> (üzerine yazılmaz): resepsiyonun notları korunur.</summary>
    private static void AppendReason(
        Domain.Entities.Reservation reservation,
        string? reason,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        var stamp = string.Create(
            CultureInfo.InvariantCulture,
            $"[{now.UtcDateTime:yyyy-MM-dd} Storno/Gast] {reason.Trim()}");

        var combined = string.IsNullOrWhiteSpace(reservation.Notes)
            ? stamp
            : reservation.Notes + "\n" + stamp;

        reservation.Notes = combined.Length > MaxNotesLength ? combined[..MaxNotesLength] : combined;
    }
}
