using HotelCore.Api.Services;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Security;

namespace HotelCore.Api.Middleware;

/// <summary>
/// Public uçların <b>IP bazlı</b> hız sınırı (api-contracts-public-booking.md §1.2).
///
/// <para><b>Bölümleme anahtarı <c>(hotelSlug, istemci IP)</c>'dir.</b> Otelin anahtara girmesi
/// şarttır: aynı IP'den iki farklı otelin sayfasına bakan bir kullanıcı tek bir kotayı
/// paylaşmamalıdır (ve bir otele yapılan saldırı diğerini kapatmamalıdır).</para>
///
/// <para><b>Aşımda 429 + <c>Retry-After</c>.</b> <c>detail</c> <b>hangi eşiğin</b> aşıldığını
/// söylemez: eşiği bilmek, saldırganın sınırın hemen altında kalmasını kolaylaştırır.</para>
///
/// <para>E-posta bazlı sınır burada uygulanamaz (gövde henüz çözümlenmemiştir); onu ilgili
/// handler'lar aynı <see cref="IPublicRateLimiter"/> portu üzerinden uygular.</para>
/// </summary>
public sealed class PublicRateLimitMiddleware(
    RequestDelegate next,
    ILogger<PublicRateLimitMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IPublicRateLimiter limiter,
        PublicClientAddress clientAddress,
        PublicTenantScope scope)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(clientAddress);
        ArgumentNullException.ThrowIfNull(scope);

        var bucket = ResolveBucket(context.Request);
        if (bucket is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var partition = (scope.HotelSlug ?? "-") + "|" + (clientAddress.Resolve(context) ?? "-");

        if (!limiter.TryAcquire(bucket, partition, out var retryAfter))
        {
            logger.PublicRateLimited(bucket, context.Request.Path);

            throw PublicApiException.RateLimited(Messages.PublicRateLimitExceeded, retryAfter);
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Yol + metot → kural adı. Eşleme <b>koda gömülü tek şeydir</b>; eşiklerin kendisi
    /// yapılandırmadan gelir.
    /// </summary>
    private static string? ResolveBucket(HttpRequest request)
    {
        var path = request.Path;
        if (!PublicTenantMiddleware.IsPublicRequest(path))
        {
            return null;
        }

        var value = path.Value ?? string.Empty;
        var method = request.Method;

        if (value.Contains("/holds", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsPost(method))
            {
                return PublicRateLimitBuckets.HoldCreate;
            }

            return HttpMethods.IsDelete(method)
                ? PublicRateLimitBuckets.HoldRelease
                : PublicRateLimitBuckets.HoldRead;
        }

        if (value.EndsWith("/bookings/lookup", StringComparison.OrdinalIgnoreCase))
        {
            return PublicRateLimitBuckets.BookingLookup;
        }

        if (value.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            return PublicRateLimitBuckets.BookingCancel;
        }

        if (value.Contains("/bookings", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsPost(method)
                ? PublicRateLimitBuckets.BookingCreate
                : PublicRateLimitBuckets.BookingRead;
        }

        if (value.Contains("/availability", StringComparison.OrdinalIgnoreCase))
        {
            return PublicRateLimitBuckets.Availability;
        }

        // Kalan her şey katalog/künye/hukuki bilgi: en yüksek eşik.
        return PublicRateLimitBuckets.Catalog;
    }
}
