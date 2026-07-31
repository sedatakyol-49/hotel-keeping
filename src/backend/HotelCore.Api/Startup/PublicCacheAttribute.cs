using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace HotelCore.Api.Startup;

/// <summary>
/// Public uçların cache politikası (api-contracts-public-booking.md §1).
///
/// <para><b>İki sınıf vardır ve arası yoktur:</b> katalog/künye/hukuki bilgi <c>public,
/// max-age=300</c> ile CDN'e verilir; müsaitlik, hold ve rezervasyon uçları <b><c>no-store</c></b>
/// olmak zorundadır. Tarihe bağlı bir sonucun ya da kişisel veri taşıyan bir yanıtın ara
/// bellekte kalması, başka bir misafire yanlış fiyat veya <b>başkasının rezervasyonunu</b>
/// göstermek demektir.</para>
///
/// <para><c>no-store</c> ayrıca <c>Vary: Accept-Language</c> ile birlikte kullanılır: aynı URL
/// farklı dillerde farklı içerik döndürür ve dil ayrımı olmadan cache'lenmesi yanlış dilde yanıt
/// üretirdi.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PublicCacheAttribute : ActionFilterAttribute
{
    /// <summary>Katalog uçlarının paylaşımlı cache süresi (saniye).</summary>
    public const int CatalogMaxAgeSeconds = 300;

    /// <summary>Yanıt CDN'de saklanabilir mi. <c>false</c> ise <c>no-store</c> yazılır.</summary>
    public bool Cacheable { get; init; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.HttpContext.Response.Headers;

        headers[HeaderNames.CacheControl] = Cacheable
            ? $"public, max-age={CatalogMaxAgeSeconds}"
            : "no-store, no-cache, must-revalidate";

        // İçerik dile göre değişir; dil ayrımı olmadan cache'lemek yanlış dilde yanıt üretir.
        headers[HeaderNames.Vary] = "Accept-Language";

        base.OnActionExecuting(context);
    }
}
