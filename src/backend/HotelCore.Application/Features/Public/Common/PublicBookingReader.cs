using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>Rezervasyon okuma satırı — sunucu içi; hiçbir GUID yanıta yazılmaz.</summary>
internal sealed record PublicBookingRow(
    PublicBooking Booking,
    Reservation Reservation,
    string RoomTypeCode,
    string RoomTypeName,
    string GuestFirstName,
    string GuestLastName,
    string? GuestEmail,
    string? GuestPhone);

/// <summary>
/// Public rezervasyon yanıtının kurulduğu tek yer.
///
/// <para><b>Alanlar dondurulmuş değerlerdir, <c>status</c> canlıdır.</b> Fiyat ve politika
/// rezervasyon anındaki anlık görüntüden okunur — otel yarın fiyatını veya iptal politikasını
/// değiştirdiğinde misafirin onayına aldığı taahhüt değişmemelidir. Durum ise operasyonel
/// gerçeği yansıtmalıdır (resepsiyon check-in yaptıysa misafir bunu görmelidir).</para>
///
/// <para><b>İç durum doğrudan verilmez:</b> <c>ReservationStatus</c> ticari/operasyonel bir
/// makinedir (<c>Option</c> gibi değerleri misafire hiçbir şey ifade etmez ve iç süreci ifşa
/// eder). Public izdüşüm 5 değerle sınırlıdır.</para>
/// </summary>
internal sealed class PublicBookingReader(IAppDbContext database, IDateTimeProvider clock)
{
    /// <summary>
    /// Erişim token'ından rezervasyonu bulur.
    /// <para>
    /// <b>404 <c>BOOKING_NOT_FOUND</c>:</b> token yok, süresi dolmuş <b>veya</b> başka otelin —
    /// <b>üçü de aynı yanıt</b>. Karşılaştırma özet üzerinden ve <b>sabit zamanlıdır</b>; otel
    /// izolasyonunu global query filter sağlar.
    /// </para>
    /// </summary>
    public async Task<PublicBookingRow> RequireByAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (!PublicTokens.IsWellFormedUrlToken(accessToken, PublicTokens.AccessTokenLength))
        {
            throw NotFound();
        }

        var hash = PublicTokens.Hash(accessToken);

        var booking = await database.PublicBookings
            .Include(row => row.Reservation)
            .FirstOrDefaultAsync(row => row.AccessTokenHash == hash, cancellationToken)
            .ConfigureAwait(false);

        if (booking is null || !PublicTokens.FixedTimeEquals(booking.AccessTokenHash, hash))
        {
            throw NotFound();
        }

        if (booking.AccessTokenExpiresAt <= clock.UtcNow)
        {
            // Kapanan şey ERİŞİMDİR, kayıt değil: veri GoBD/AO §147 gereği saklanmaya devam eder.
            throw NotFound();
        }

        return await LoadRowAsync(booking, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Yüklenmiş rezervasyondan yanıtı kurar.</summary>
    public PublicBookingResponse BuildResponse(
        PublicHotelContext hotel,
        PublicBookingRow row,
        string? rawAccessToken)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(row);

        var booking = row.Booking;
        var reservation = row.Reservation;
        var price = PublicJson.Deserialize<PublicPriceResponse>(booking.PriceSnapshotJson)
                    ?? new PublicPriceResponse();
        var policy = PublicJson.Deserialize<PublicCancellationPolicyResponse>(
                         booking.CancellationPolicySnapshotJson)
                     ?? new PublicCancellationPolicyResponse();
        var legal = PublicJson.Deserialize<PublicLegalNoticesResponse>(booking.LegalSnapshotJson)
                    ?? new PublicLegalNoticesResponse();

        var status = MapStatus(reservation.Status);
        var now = clock.UtcNow;

        return new PublicBookingResponse
        {
            BookingReference = PublicTokens.FormatBookingReference(booking.BookingReference),

            // Ham token YALNIZCA 201 yanıtında doludur; sonraki okumalarda null'dır.
            AccessToken = rawAccessToken,
            AccessTokenExpiresAt = hotel.ToHotelLocal(booking.AccessTokenExpiresAt),
            Status = status,
            CreatedAt = hotel.ToHotelLocal(booking.CreatedAt),
            Hotel = new PublicBookingHotelResponse
            {
                Slug = hotel.Hotel.PublicSlug ?? string.Empty,
                Name = hotel.Hotel.Name,
                AddressLine = hotel.Hotel.AddressLine,
                PostalCode = hotel.Hotel.PostalCode,
                City = hotel.Hotel.City,
                Country = hotel.Hotel.Country.ToString(),
                Phone = hotel.Hotel.Phone,
                Email = hotel.Hotel.Email,
                TimeZoneId = hotel.Hotel.TimeZoneId
            },
            Stay = new PublicBookingStayResponse
            {
                RoomTypeCode = row.RoomTypeCode,
                RoomTypeName = row.RoomTypeName,
                CheckIn = reservation.CheckIn,
                CheckOut = reservation.CheckOut,
                Nights = reservation.CheckOut.DayNumber - reservation.CheckIn.DayNumber,
                Adults = reservation.Adults,
                Children = reservation.Children,
                CheckInFromLocal = hotel.Hotel.CheckInFromLocal,
                CheckOutUntilLocal = hotel.Hotel.CheckOutUntilLocal,
                EstimatedArrivalLocalTime = booking.EstimatedArrivalLocalTime
            },
            Guest = new PublicBookingGuestResponse
            {
                FirstName = row.GuestFirstName,
                LastName = row.GuestLastName,
                Email = row.GuestEmail ?? string.Empty,
                Phone = row.GuestPhone
            },
            Price = price,
            Cancellation = new PublicBookingCancellationResponse
            {
                Type = policy.Type,
                FreeCancellationUntil = policy.FreeCancellationUntil,

                // Politika donmuştur ama "şu anda ücretsiz mi" sorusu ZAMANA bağlıdır ve her
                // okumada yeniden değerlendirilir; aksi hâlde rezervasyon anındaki cevap
                // sonsuza dek gösterilirdi.
                IsFreeCancellationAvailable = now <= policy.FreeCancellationUntil,
                LateCancellationFeePercent = policy.LateCancellationFeePercent,
                LateCancellationFeeAmount = policy.LateCancellationFeeAmount,
                NoShowFeePercent = policy.NoShowFeePercent,
                NoShowFeeAmount = policy.NoShowFeeAmount,
                CityTaxRefundedOnCancellation = true,
                PolicyTextKey = policy.PolicyTextKey,
                CanCancelOnline = CanCancelOnline(reservation.Status),
                ChargedFeeAmount = booking.CancellationFeeAmount
            },
            Payment = new PublicBookingPaymentResponse
            {
                Method = PublicPaymentOptions.PayAtPropertyMethod,
                AmountDueAtProperty = price.AmountDueAtProperty,
                PrepaidAmount = price.PrepaidAmount,
                Guarantee = null
            },
            Legal = legal,
            Confirmation = new PublicBookingConfirmationResponse
            {
                Channel = "Email",
                RecipientMasked = PublicTokens.MaskEmail(row.GuestEmail),
                SentAt = booking.ConfirmationSentAt is DateTimeOffset sentAt
                    ? hotel.ToHotelLocal(sentAt)
                    : null,
                DocumentVersion = booking.ConfirmationDocumentVersion,
                Culture = booking.ConfirmationCulture ?? booking.Culture
            }
        };
    }

    /// <summary>Rezervasyona bağlı yardımcı satırları (oda tipi, misafir) yükler.</summary>
    public async Task<PublicBookingRow> LoadRowAsync(
        PublicBooking booking,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var details = await database.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.Id == booking.ReservationId)
            .Select(reservation => new
            {
                RoomTypeCode = reservation.Room.RoomType.Code,
                RoomTypeName = reservation.Room.RoomType.Name,
                reservation.Guest.FirstName,
                reservation.Guest.LastName,
                reservation.Guest.Email,
                reservation.Guest.Phone
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? throw NotFound();

        var reservation = booking.Reservation
                          ?? await database.Reservations
                              .FirstOrDefaultAsync(
                                  candidate => candidate.Id == booking.ReservationId,
                                  cancellationToken)
                              .ConfigureAwait(false)
                          ?? throw NotFound();

        return new PublicBookingRow(
            booking,
            reservation,
            details.RoomTypeCode,
            details.RoomTypeName,
            details.FirstName,
            details.LastName,
            details.Email,
            details.Phone);
    }

    /// <summary>İç durumun public izdüşümü (api-contracts-public-booking.md §7.2).</summary>
    public static string MapStatus(ReservationStatus status) => status switch
    {
        ReservationStatus.Option or ReservationStatus.Confirmed => "Confirmed",
        ReservationStatus.CheckedIn => "InHouse",
        ReservationStatus.CheckedOut => "Completed",
        ReservationStatus.Cancelled => "Cancelled",
        ReservationStatus.NoShow => "NoShow",
        _ => "Confirmed"
    };

    /// <summary>
    /// Giriş yapılmış veya tamamlanmış konaklama online iptal edilemez: bu noktadan sonra iptal
    /// bir <i>ticari</i> karardır (erken çıkış, ücret pazarlığı) ve resepsiyona aittir.
    /// </summary>
    public static bool CanCancelOnline(ReservationStatus status) =>
        status is ReservationStatus.Option or ReservationStatus.Confirmed;

    private static PublicApiException NotFound() =>
        PublicApiException.NotFound(PublicErrorCodes.BookingNotFound, Messages.PublicBookingNotFound);
}
