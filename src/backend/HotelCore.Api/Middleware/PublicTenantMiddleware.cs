using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Security;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

namespace HotelCore.Api.Middleware;

/// <summary>
/// Misafire açık kanalın tenant kapsamını kurar: yoldaki <c>hotelSlug</c> → <c>HotelId</c>
/// (architecture-public-booking.md §4.1).
///
/// <para><b>Neden yol parametresi</b> (header ya da host değil): URL, SEO'nun ve CDN cache
/// anahtarının <i>kendisidir</i>. Header CDN cache anahtarına girmez — tüm oteller aynı URL'de
/// görünür ve yanlış otelin sayfası cache'ten servis edilebilirdi; crawler zaten header
/// göndermez. Host ise her otel için DNS + TLS gerektirir ve yerelde test edilemez.</para>
///
/// <para><b>Slug çözülemezse istek reddedilmez, kapsam BOŞ bırakılır.</b> 404'ü handler üretir
/// (<c>PublicHotelReader</c>): böylece yanıt isteğin dilinde döner (bu middleware localization'dan
/// önce çalışır) ve "slug yok / silinmiş / kanal kapalı" üç durumu tek bir yanıtta birleşir.
/// Kapsamın boş kalması güvenlidir: <see cref="PublicTenantScope.IsPublicRequest"/> kimliği
/// bastırdığı için hiçbir tenant satırı görünmez.</para>
///
/// <para><b><c>X-Hotel-Id</c> public yolda YOK SAYILIR</b> (400 üretmez): otorite yoldadır ve
/// public istemci bu header'ı göndermez. Bu yüzden <c>HotelContextMiddleware</c> de public
/// yollarda atlanır.</para>
/// </summary>
public sealed class PublicTenantMiddleware(RequestDelegate next)
{
    /// <summary>Public API yüzeyinin ön eki.</summary>
    public const string PublicPathPrefix = "/api/v1/public";

    /// <summary>Otel kapsamlı public yolların ön eki (<c>/brands/...</c> hariç).</summary>
    private const string HotelPathPrefix = PublicPathPrefix + "/hotels/";

    public async Task InvokeAsync(HttpContext context, PublicTenantScope scope, IAppDbContext database)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(database);

        if (!IsPublicRequest(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Marka ucunda otel yoktur ama istek yine PUBLIC'tir: kimlik bastırılmalıdır.
        scope.MarkPublicRequest();

        var slug = ExtractHotelSlug(context.Request.Path);
        if (slug is not null)
        {
            var hotel = await database.Hotels
                .AsNoTracking()
                .Where(candidate => candidate.PublicSlug == slug && candidate.PublicBookingSettings.IsEnabled)
                .Select(candidate => new { candidate.Id, candidate.DefaultCulture })
                .FirstOrDefaultAsync(context.RequestAborted)
                .ConfigureAwait(false);

            if (hotel is not null)
            {
                scope.Activate(hotel.Id, slug, hotel.DefaultCulture);
            }
        }

        using (LogContext.PushProperty("PublicHotelSlug", slug))
        using (LogContext.PushProperty("HotelId", scope.HotelId))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    /// <summary>İstek public API yüzeyine mi ait.</summary>
    public static bool IsPublicRequest(PathString path) =>
        path.StartsWithSegments(PublicPathPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>/api/v1/public/hotels/{hotelSlug}/...</c> yolundan slug'ı çıkarır.
    /// <para>
    /// Biçim <c>Hotel.PublicSlug</c> kuralıyla aynıdır: küçük harf, <c>a-z0-9-</c>, 3–60 karakter.
    /// Biçim tutmuyorsa veritabanına <b>hiç gidilmez</b> — geçersiz slug'larla sorgu tetiklemek
    /// ucuz bir hizmet reddi vektörüdür.
    /// </para>
    /// </summary>
    public static string? ExtractHotelSlug(PathString path)
    {
        var value = path.Value;
        if (value is null || !value.StartsWith(HotelPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var remainder = value[HotelPathPrefix.Length..];
        var end = remainder.IndexOf('/', StringComparison.Ordinal);
        var slug = end < 0 ? remainder : remainder[..end];

        return IsWellFormedSlug(slug) ? slug : null;
    }

    private static bool IsWellFormedSlug(string slug)
    {
        if (slug.Length is < 3 or > 60)
        {
            return false;
        }

        foreach (var character in slug)
        {
            var valid = char.IsAsciiDigit(character) || char.IsAsciiLetterLower(character) || character is '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
