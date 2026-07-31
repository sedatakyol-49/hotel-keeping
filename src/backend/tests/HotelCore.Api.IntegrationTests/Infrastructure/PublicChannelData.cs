using System.Globalization;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Public kanal ve cakisma kisiti testleri icin <b>ham entity</b> ureticileri.
/// <para>
/// Testler bilincli olarak handler'lari degil dogrudan <c>DbContext</c>'i kullanir: amac
/// uygulamanin on kontrolunu degil <b>veritabani kisitini</b> dogrulamaktir. Ustteki katman
/// atlanmazsa kisitin gercekten var olup olmadigi hicbir zaman gorulmez.
/// </para>
/// </summary>
internal static class PublicChannelData
{
    /// <summary>Rezervasyon numarasi otel icinde benzersizdir; testler carpismasin diye sayac.</summary>
    private static int _sequence;

    public static Reservation Reservation(
        Guid hotelId,
        Guid roomId,
        Guid guestId,
        DateOnly checkIn,
        DateOnly checkOut,
        ReservationStatus status = ReservationStatus.Confirmed,
        bool isDeleted = false,
        ReservationChannel channel = ReservationChannel.Direct) => new()
        {
            HotelId = hotelId,
            RoomId = roomId,
            GuestId = guestId,
            ReservationNumber = NextReservationNumber(),
            CheckIn = checkIn,
            CheckOut = checkOut,
            Adults = 2,
            Status = status,
            Channel = channel,
            TotalAmount = 240m,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
        };

    /// <summary>Otel ici benzersiz, en fazla 32 karakter (kolon sinirlari).</summary>
    public static string NextReservationNumber()
    {
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"IT-{Interlocked.Increment(ref _sequence):00000}-{Guid.NewGuid():N}");

        return value[..32];
    }

    public static BookingHold Hold(
        Guid hotelId,
        Guid roomTypeId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? consumedAt = null,
        Guid? consumedByReservationId = null,
        string? tokenHash = null) => new()
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            RoomId = roomId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Adults = 2,
            TokenHash = tokenHash ?? Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(15),
            ConsumedAt = consumedAt,
            ConsumedByReservationId = consumedByReservationId,
            Currency = "EUR",
            Culture = "de",
            AccommodationGross = 450m,
            CityTaxAmount = 18m,
            TotalGross = 468m,
            PriceSnapshotJson = "{}",
            CancellationPolicySnapshotJson = "{}",
            OrderSummaryJson = "{}",
            LegalSnapshotJson = "{}",
            SummaryHash = "sha256:" + new string('a', 64)
        };

    public static PublicBooking Booking(
        Guid hotelId,
        Guid reservationId,
        string bookingReference,
        string accessTokenHash,
        bool isDeleted = false) => new()
        {
            HotelId = hotelId,
            ReservationId = reservationId,
            BookingReference = bookingReference,
            AccessTokenHash = accessTokenHash,
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(60),
            Culture = "de",
            TermsAccepted = true,
            TermsVersion = "2026-07-01",
            PrivacyNoticeAcknowledged = true,
            PrivacyNoticeVersion = "2026-07-01",
            WithdrawalNoticeAcknowledged = true,
            WithdrawalNoticeVersion = "2026-07-01",
            BookerIsAdult = true,
            MarketingOptIn = false,
            ConsentRecordedAt = DateTimeOffset.UtcNow,
            OrderButtonLabel = "zahlungspflichtig buchen",
            SummaryHash = "sha256:" + new string('b', 64),
            OrderSummaryJson = "{}",
            PriceSnapshotJson = "{}",
            CancellationPolicySnapshotJson = "{}",
            LegalSnapshotJson = "{}",
            ConfirmationMode = PublicBookingConfirmationMode.Instant,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
        };
}
