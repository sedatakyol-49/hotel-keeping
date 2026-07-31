using System.Globalization;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Options;
using HotelCore.Application.Features.Public.Common;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.CreateBooking;

/// <summary>
/// Rezervasyonu oluşturur — public kanalın <b>tek yazma</b> ucu.
///
/// <para><b>Sıra bilinçlidir ve tek transaction'dır:</b>
/// <list type="number">
///   <item>bot doğrulaması ve e-posta bazlı hız sınırı (kötüye kullanım savunması),</item>
///   <item>hold çözülür: süresi dolmuşsa <c>409 HOLD_EXPIRED</c>, tüketilmişse
///   <c>409 HOLD_ALREADY_USED</c>,</item>
///   <item><c>checkout.summaryHash</c> hold'daki değerle karşılaştırılır → <c>409
///   SUMMARY_CHANGED</c> (§312j Abs. 2'nin makineyle zorlanabilir kısmı),</item>
///   <item>onaylanan hukuki versiyonlar <b>güncel</b> mi → <c>409 LEGAL_TEXT_CHANGED</c>,</item>
///   <item><c>Guest</c> + <c>Reservation</c> + <c>Folio</c>/<c>RoomCharge</c> +
///   <c>PublicBooking</c> yazılır, hold tüketilir — <b>tek <c>SaveChanges</c></b>,</item>
///   <item>commit sonrası onay e-postası <b>outbox'a</b> konur.</item>
/// </list></para>
///
/// <para><b>Fiyat istekten alınmaz.</b> <c>Reservation.TotalAmount</c> hold'da <b>donmuş</b>
/// <c>accommodationGross</c>'tur; kişi sayısı ve tarihler de hold'dan okunur. İstemcinin araya
/// girip fiyatı etkileyen bir değeri değiştirmesi mümkün değildir.</para>
///
/// <para><b><c>Guest</c> her rezervasyonda YENİ açılır</b>, e-postaya göre birleştirilmez
/// (architecture-public-booking.md §9.6): birleştirme, e-postayı bilen herkesin başka birinin
/// konaklama geçmişine bağlanmasına ve yanlış kişiye konaklama yazılmasına yol açardı.</para>
/// </summary>
internal sealed class PublicCreateBookingHandler(
    IAppDbContext database,
    PublicHotelReader hotels,
    PublicHoldService holds,
    PublicLegalReader legal,
    PublicBookingReader bookings,
    ReservationNumberGenerator numbers,
    ReservationFolioService folios,
    IDateTimeProvider clock,
    IBotChallengeVerifier botChallenge,
    IPublicRateLimiter rateLimiter,
    IBookingConfirmationOutbox outbox,
    PublicChannelOptions options)
    : IRequestHandler<PublicCreateBookingRequest, PublicBookingResponse>
{
    public async Task<PublicBookingResponse> Handle(
        PublicCreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        EnsurePaymentSupported(request.Payment);
        await EnsureNotABotAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureEmailRateLimit(context, request.Guest.Email);

        var hold = await holds.FindAsync(request.HoldToken, cancellationToken).ConfigureAwait(false);
        holds.EnsureUsable(hold);

        // §312j Abs. 2: misafirin onayladığı özet ile sunucunun dondurduğu özet AYNI olmalıdır.
        if (!string.Equals(hold!.SummaryHash, request.Checkout.SummaryHash, StringComparison.Ordinal))
        {
            throw PublicApiException.Conflict(PublicErrorCodes.SummaryChanged, Messages.PublicSummaryChanged);
        }

        await EnsureLegalVersionsCurrentAsync(context, request.Consents, cancellationToken)
            .ConfigureAwait(false);

        var guest = BuildGuest(context.HotelId, request);
        database.Guests.Add(guest);

        var reservation = new Reservation
        {
            HotelId = context.HotelId,
            RoomId = hold.RoomId,
            Guest = guest,
            CheckIn = hold.CheckIn,
            CheckOut = hold.CheckOut,
            Adults = hold.Adults,
            Children = hold.Children,
            Status = ReservationStatus.Confirmed,
            Channel = ReservationChannel.Website,

            // Donmuş konaklama tutarı — Kurtaxe DAHİL DEĞİL (fatura onu ayrı satır olarak üretir).
            TotalAmount = hold.AccommodationGross,
            DepositPercent = 0m,
            Notes = BuildNotes(request.Stay.GuestNote)
        };

        database.Reservations.Add(reservation);

        var nights = hold.CheckOut.DayNumber - hold.CheckIn.DayNumber;
        await folios.SyncRoomChargeAsync(reservation, nights, cancellationToken).ConfigureAwait(false);

        var accessToken = PublicTokens.NewAccessToken();
        var publicBooking = BuildPublicBooking(context, hold, request, reservation, accessToken, now);
        database.PublicBookings.Add(publicBooking);

        hold.ConsumedAt = now;
        hold.ConsumedByReservation = reservation;

        await SaveAsync(reservation, context.HotelId, cancellationToken).ConfigureAwait(false);

        var row = await bookings.LoadRowAsync(publicBooking, cancellationToken).ConfigureAwait(false);
        var response = bookings.BuildResponse(context, row, accessToken);

        // COMMIT SONRASI: gönderim hatası rezervasyonu geri almaz (§312f). Outbox asla fırlatmaz.
        outbox.Enqueue(new BookingConfirmationMessage(
            publicBooking.Id,
            context.HotelId,
            context.Hotel.PublicSlug ?? string.Empty,
            publicBooking.BookingReference,
            accessToken,
            guest.Email ?? string.Empty,
            publicBooking.Culture,
            options.ConfirmationDocumentVersion,
            BuildConfirmationBody(response)));

        return response;
    }

    /// <summary>
    /// Bu fazda yalnızca "girişte ödeme" sunulur. Desteklenmeyen bir yöntem/garanti isteği
    /// <b>sessizce yok sayılmaz</b>: sözleşme yalan söylemez, <c>400 CHANNEL_NOT_CONFIGURED</c>.
    /// </summary>
    private static void EnsurePaymentSupported(PublicPaymentRequest payment)
    {
        if (!string.Equals(payment.Method, PublicPaymentOptions.PayAtPropertyMethod, StringComparison.Ordinal))
        {
            throw PublicApiException.BadRequest(
                PublicErrorCodes.ChannelNotConfigured,
                Messages.PublicPaymentMethodNotOffered);
        }

        if (!string.IsNullOrWhiteSpace(payment.Guarantee))
        {
            throw PublicApiException.BadRequest(
                PublicErrorCodes.ChannelNotConfigured,
                Messages.PublicChannelNotConfigured);
        }
    }

    private async Task EnsureNotABotAsync(
        PublicCreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var verified = await botChallenge
            .VerifyAsync(request.ChallengeToken, request.ClientIp, cancellationToken)
            .ConfigureAwait(false);

        if (!verified)
        {
            throw PublicApiException.BadRequest(
                PublicErrorCodes.ValidationFailed,
                Messages.PublicBotChallengeFailed);
        }
    }

    /// <summary>
    /// E-posta bazlı hız sınırı. Anahtar <c>(hotelSlug, SHA-256(lower(email)))</c>'dir —
    /// <b>ham e-posta hız sınırı deposunda saklanmaz</b>.
    /// </summary>
    private void EnsureEmailRateLimit(PublicHotelContext context, string email)
    {
        var key = context.Hotel.PublicSlug + "|" + PublicTokens.HashEmail(email);

        if (!rateLimiter.TryAcquire(PublicRateLimitBuckets.BookingCreateEmail, key, out var retryAfter))
        {
            throw PublicApiException.RateLimited(Messages.PublicRateLimitExceeded, retryAfter);
        }
    }

    /// <summary>
    /// Onaylanan AGB/aydınlatma/cayma versiyonları otelin <b>güncel</b> yayınıyla eşleşmelidir.
    /// Eşleşmezse <c>409 LEGAL_TEXT_CHANGED</c>: misafirin okumadığı bir metne dayanarak sözleşme
    /// kurulamaz (DSGVO Art. 7 Abs. 1 ve §312j Abs. 2'nin ruhu).
    /// </summary>
    private async Task EnsureLegalVersionsCurrentAsync(
        PublicHotelContext context,
        PublicConsentsRequest consents,
        CancellationToken cancellationToken)
    {
        var versions = await legal
            .GetActiveVersionsAsync(RequestCulture.Current, context.Hotel.DefaultCulture, cancellationToken)
            .ConfigureAwait(false);

        var mismatched =
            IsStale(versions, PublicLegalDocumentKeys.Terms, consents.TermsVersion)
            || IsStale(versions, PublicLegalDocumentKeys.Privacy, consents.PrivacyNoticeVersion)
            || IsStale(versions, PublicLegalDocumentKeys.Withdrawal, consents.WithdrawalNoticeVersion);

        if (mismatched)
        {
            throw PublicApiException.Conflict(
                PublicErrorCodes.LegalTextChanged,
                Messages.PublicLegalTextChanged);
        }
    }

    /// <summary>
    /// Otel o belgeyi hiç yayımlamamışsa (versiyon yok) rıza versiyonu da aranmaz: kanal açılırken
    /// belgelerin yayımlanması bir ayar sorumluluğudur, misafirin isteği bu yüzden reddedilmez.
    /// </summary>
    private static bool IsStale(
        IReadOnlyDictionary<string, string> versions,
        string key,
        string? acknowledgedVersion) =>
        versions.TryGetValue(key, out var current)
        && !string.Equals(current, acknowledgedVersion, StringComparison.Ordinal);

    private static Guest BuildGuest(Guid hotelId, PublicCreateBookingRequest request) => new()
    {
        HotelId = hotelId,
        FirstName = request.Guest.FirstName.Trim(),
        LastName = request.Guest.LastName.Trim(),
        Email = request.Guest.Email.Trim(),
        Phone = Normalize(request.Guest.Phone),
        Culture = SupportedCultures.Normalize(request.Guest.Culture),

        // Meldeschein verisi rezervasyon anında TOPLANMAZ (BMG §§29–30): uyrukluk ve doğum
        // tarihi girişte alınır. Boş bırakmak bir eksiklik değil, amaç sınırlamasının gereğidir.
        Nationality = null,
        BirthDate = null,

        // Adres bileşenleri yalnızca kurumsal fatura istendiyse kopyalanır.
        AddressLine = Normalize(request.InvoiceAddress?.AddressLine),
        PostalCode = Normalize(request.InvoiceAddress?.PostalCode),
        City = Normalize(request.InvoiceAddress?.City)
    };

    /// <summary>
    /// Misafir notu rezervasyon notlarına <b>damgayla</b> eklenir: resepsiyon, metnin misafirden
    /// geldiğini (yani doğrulanmamış olduğunu) görmelidir.
    /// </summary>
    private static string? BuildNotes(string? guestNote) =>
        string.IsNullOrWhiteSpace(guestNote) ? null : "[Gast] " + guestNote.Trim();

    private PublicBooking BuildPublicBooking(
        PublicHotelContext context,
        BookingHold hold,
        PublicCreateBookingRequest request,
        Reservation reservation,
        string accessToken,
        DateTimeOffset now)
    {
        var booking = new PublicBooking
        {
            HotelId = context.HotelId,
            Reservation = reservation,
            BookingReference = PublicTokens.NewBookingReference(),
            AccessTokenHash = PublicTokens.Hash(accessToken),
            // Son tarih otelin YEREL takviminde hesaplanır (çıkış + 30 gün, gece yarısı), ama
            // veritabanına UTC yazılır: Npgsql "timestamp with time zone" kolonuna yalnızca
            // offset 0 kabul eder. Yanıt bunu tekrar otel yerel offset'ine çevirir.
            AccessTokenExpiresAt = PublicTimeZone.ToInstant(
                hold.CheckOut.AddDays(options.AccessTokenValidityDaysAfterCheckOut),
                TimeOnly.MinValue,
                context.TimeZone).ToUniversalTime(),
            Culture = SupportedCultures.Normalize(request.Guest.Culture),
            CountryOfResidence = ParseCountry(request.Guest.CountryOfResidence),
            EstimatedArrivalLocalTime = request.Stay.EstimatedArrivalLocalTime,
            InvoiceAddress = BuildInvoiceAddress(request.InvoiceAddress),

            TermsAccepted = request.Consents.TermsAccepted,
            TermsVersion = request.Consents.TermsVersion,
            PrivacyNoticeAcknowledged = request.Consents.PrivacyNoticeAcknowledged,
            PrivacyNoticeVersion = request.Consents.PrivacyNoticeVersion,
            WithdrawalNoticeAcknowledged = request.Consents.WithdrawalNoticeAcknowledged,
            WithdrawalNoticeVersion = request.Consents.WithdrawalNoticeVersion,
            BookerIsAdult = request.Consents.BookerIsAdult,
            MarketingOptIn = request.Consents.MarketingOptIn,
            ConsentRecordedAt = now,

            // §312j Abs. 3 kanıtı: gösterilen metin DOĞRULANMAZ, kaydedilir.
            OrderButtonLabel = request.Checkout.OrderButtonLabel.Trim(),
            SummaryHash = hold.SummaryHash,

            // Anlık görüntüler hold'dan KOPYALANIR: otel yarın fiyatını/AGB'sini değiştirdiğinde
            // geçmiş rezervasyonun kanıtı değişmemelidir.
            OrderSummaryJson = hold.OrderSummaryJson,
            PriceSnapshotJson = hold.PriceSnapshotJson,
            CancellationPolicySnapshotJson = hold.CancellationPolicySnapshotJson,
            LegalSnapshotJson = hold.LegalSnapshotJson,
            ConfirmationMode = context.Hotel.PublicBookingSettings.ConfirmationMode,
            ConfirmationDocumentVersion = options.ConfirmationDocumentVersion,
            ConfirmationCulture = SupportedCultures.Normalize(request.Guest.Culture)
        };

        return booking;
    }

    private static PublicInvoiceAddress BuildInvoiceAddress(PublicInvoiceAddressRequest? source) =>
        source is null
            ? new PublicInvoiceAddress()
            : new PublicInvoiceAddress
            {
                Company = Normalize(source.Company),
                AddressLine = Normalize(source.AddressLine),
                PostalCode = Normalize(source.PostalCode),
                City = Normalize(source.City),
                Country = ParseCountry(source.Country),
                VatId = Normalize(source.VatId)
            };

    private static Country? ParseCountry(string? value) =>
        Enum.TryParse<Country>(value, ignoreCase: true, out var country) ? country : null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Kaydeder. İki farklı yarış durumu vardır ve ayrımı <b>sonuçtan</b> yapılır: numara
    /// çarpışmasında yeni numarayla tekrar denenir (boşluk sorun değildir), oda çakışmasında
    /// yeniden denemek anlamsızdır ve misafire <c>409 ROOM_NO_LONGER_AVAILABLE</c> döner.
    /// <para>
    /// <b>Neden mesaj/SQLSTATE ayrıştırması yapılmıyor:</b> <c>ConflictException</c> kısıt adını
    /// bilinçli olarak taşımaz (şema detayı istemciye sızmasın diye). Denemeler tükendiğinde
    /// tek anlamlı yorum "oda artık alınamıyor"dur; en fazla birkaç başarısız INSERT maliyeti
    /// karşılığında hata kataloğu doğru kalır.
    /// </para>
    /// </summary>
    private async Task SaveAsync(Reservation reservation, Guid hotelId, CancellationToken cancellationToken)
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
            catch (ConflictException exception)
            {
                if (attempt >= ReservationNumberGenerator.MaxAttempts)
                {
                    throw new PublicApiException(
                        409,
                        PublicErrorCodes.RoomNoLongerAvailable,
                        Messages.PublicRoomNoLongerAvailable,
                        innerException: exception);
                }
            }
        }
    }

    /// <summary>
    /// §312f BGB zorunlu içeriği (architecture-public-booking.md §9.8) — <b>gövdede</b>, yalnızca
    /// bağlantı olarak değil. Metin dil-nötr ASCII'dir; zengin şablon
    /// <c>IBookingConfirmationSender</c> implementasyonunun işidir, ama <b>hangi kalemlerin
    /// bulunmak zorunda olduğu</b> burada, tek yerde tanımlıdır.
    /// </summary>
    private static string BuildConfirmationBody(PublicBookingResponse response)
    {
        var price = response.Price;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
             Booking reference: {response.BookingReference}
             Hotel: {response.Hotel.Name}, {response.Hotel.AddressLine}, {response.Hotel.PostalCode} {response.Hotel.City} ({response.Hotel.Country})
             Contact: {response.Hotel.Phone} / {response.Hotel.Email}
             Room type: {response.Stay.RoomTypeName} ({response.Stay.RoomTypeCode})
             Occupancy: {response.Stay.Adults} adult(s), {response.Stay.Children} child(ren)
             Stay: {response.Stay.CheckIn:yyyy-MM-dd} (from {response.Stay.CheckInFromLocal:HH\:mm}) - {response.Stay.CheckOut:yyyy-MM-dd} (until {response.Stay.CheckOutUntilLocal:HH\:mm}), {response.Stay.Nights} night(s)
             Accommodation (incl. VAT {price.AccommodationVatRate}%): {price.AccommodationGross} {price.Currency}
             City tax (not subject to VAT): {price.CityTax.Amount} {price.Currency}
             Total price (incl. VAT and all mandatory charges): {price.TotalGross} {price.Currency}
             Payment: at the property, {response.Payment.AmountDueAtProperty} {price.Currency}
             Free cancellation until: {response.Cancellation.FreeCancellationUntil:yyyy-MM-ddTHH:mm:sszzz}
             Late cancellation fee: {response.Cancellation.LateCancellationFeePercent}% of the accommodation price ({response.Cancellation.LateCancellationFeeAmount} {price.Currency}); city tax is not charged.
             Right of withdrawal: does not apply ({response.Legal.WithdrawalRight.LegalBasis}); notice version {response.Legal.WithdrawalRight.NoticeVersion}.
             Terms accepted: version {response.Legal.Terms.Version}
             """);
    }
}
