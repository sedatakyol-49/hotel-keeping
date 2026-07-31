using System.Diagnostics;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Options;
using HotelCore.Application.Features.Public.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.LookupBooking;

/// <summary>
/// Erişim bağlantısını yeniden gönderir.
///
/// <para><b>Numaralandırma koruması üç katmanlıdır:</b>
/// <list type="number">
///   <item><b>Gövde yok:</b> yanıt her zaman <c>202</c>, hiçbir alan taşımaz.</item>
///   <item><b>Zamanlama sabit:</b> eşleşme bulunsa da bulunmasa da işlem en az
///   <c>LookupMinimumResponseMilliseconds</c> sürer. Aksi hâlde "hızlı 202" = "kayıt yok",
///   "yavaş 202" = "kayıt var" olurdu ve gövdesizlik hiçbir işe yaramazdı.</item>
///   <item><b>Hız sınırı:</b> IP ve e-posta özeti başına saatlik eşik (uçtaki middleware +
///   handler).</item>
/// </list></para>
///
/// <para><b>Yeni erişim token'ı üretilmez:</b> mevcut token'ın <i>ham</i> hâli sunucuda yoktur
/// (yalnızca özeti saklanır), bu yüzden bağlantı ancak yeni bir token atanarak gönderilebilir.
/// Token yenilemek, e-postayı bilen birinin eski bağlantıyı geçersiz kılmasına izin verirdi —
/// bu bir hizmet reddi vektörüdür. Bu yüzden <b>yeni bir token üretilir ve eskisi de geçerli
/// kalmaz</b>: kayıt sahibi zaten e-posta kutusuna erişebilen kişidir.</para>
/// </summary>
internal sealed class PublicLookupBookingHandler(
    IAppDbContext database,
    PublicHotelReader hotels,
    IPublicRateLimiter rateLimiter,
    IBookingConfirmationOutbox outbox,
    IDateTimeProvider clock,
    PublicChannelOptions options)
    : IRequestHandler<PublicLookupBookingRequest, Unit>
{
    public async Task<Unit> Handle(PublicLookupBookingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = Stopwatch.GetTimestamp();
        var context = await hotels.RequireCurrentAsync(cancellationToken).ConfigureAwait(false);

        EnsureEmailRateLimit(context, request.Email);

        try
        {
            await TrySendAccessLinkAsync(context, request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await PadResponseTimeAsync(started, cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }

    private async Task TrySendAccessLinkAsync(
        PublicHotelContext context,
        PublicLookupBookingRequest request,
        CancellationToken cancellationToken)
    {
        var reference = PublicTokens.NormalizeBookingReference(request.BookingReference);
        if (reference is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return;
        }

        var email = request.Email.Trim();
        var now = clock.UtcNow;

        // Tenant filtresi işi yapar: başka otelin referansı bu otelin yolunda bulunamaz.
        var booking = await database.PublicBookings
            .FirstOrDefaultAsync(
                candidate => candidate.BookingReference == reference
                             && candidate.AccessTokenExpiresAt > now,
                cancellationToken)
            .ConfigureAwait(false);

        if (booking is null)
        {
            return;
        }

        var normalizedEmail = email.ToLowerInvariant();

        // CA1304/CA1311/CA1862 bastırılır: karşılaştırma veritabanında (`lower("Email") = @p`)
        // yapılır; StringComparison alan aşırı yüklemeler EF Core tarafından çevrilemez.
#pragma warning disable CA1304, CA1311, CA1862
        var matchesEmail = await database.Reservations
            .AsNoTracking()
            .AnyAsync(
                reservation => reservation.Id == booking.ReservationId
                               && reservation.Guest.Email != null
                               && reservation.Guest.Email.ToLower() == normalizedEmail,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA1304, CA1311, CA1862

        if (!matchesEmail)
        {
            return;
        }

        // Ham token saklanmadığı için yeniden gönderilemez; yeni bir token atanır.
        var accessToken = PublicTokens.NewAccessToken();
        booking.AccessTokenHash = PublicTokens.Hash(accessToken);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        outbox.Enqueue(new BookingAccessLinkMessage(
            booking.Id,
            context.Hotel.PublicSlug ?? string.Empty,
            booking.BookingReference,
            accessToken,
            email,
            booking.Culture));
    }

    private void EnsureEmailRateLimit(PublicHotelContext context, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var key = context.Hotel.PublicSlug + "|" + PublicTokens.HashEmail(email);

        if (!rateLimiter.TryAcquire(PublicRateLimitBuckets.BookingLookupEmail, key, out var retryAfter))
        {
            throw PublicApiException.RateLimited(Messages.PublicRateLimitExceeded, retryAfter);
        }
    }

    /// <summary>Sabit gecikme profili — yanıt süresi bir rezervasyonun varlığını sızdırmamalıdır.</summary>
    private async Task PadResponseTimeAsync(long startedTimestamp, CancellationToken cancellationToken)
    {
        var minimum = TimeSpan.FromMilliseconds(options.LookupMinimumResponseMilliseconds);
        var elapsed = Stopwatch.GetElapsedTime(startedTimestamp);

        if (elapsed < minimum)
        {
            await Task.Delay(minimum - elapsed, cancellationToken).ConfigureAwait(false);
        }
    }
}
