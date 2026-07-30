using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Net.Http.Headers;

namespace HotelCore.Api.Startup;

/// <summary>
/// Hata yanıtlarının <b>başlıklarını</b> sözleşmeye uygun hâle getirir:
/// <c>Content-Language</c> ve <c>application/problem+json; charset=utf-8</c>.
///
/// <para><b>Neden gerekli — <c>Content-Language</c>:</b> <c>UseRequestLocalization</c>
/// (<c>ApplyCurrentCultureToResponseHeaders</c>) başlığı yazar, fakat bir istisna yukarı
/// kabardığında <c>ExceptionHandlerMiddleware</c> yanıtı <b>sıfırlar</b>
/// (<c>Response.Clear()</c>) — yazılmış tüm başlıklar silinir. Sonuç: <c>200</c> yanıtı
/// <c>Content-Language: tr</c> taşırken <c>400</c>/<c>409</c> ProblemDetails yanıtları hiçbir dil
/// bildirmez; yani başlık <b>en çok gerektiği yerde</b> yoktur (istemci hangi dilde bir
/// <c>detail</c> aldığını bilemez, önbellek de dile göre ayrıştıramaz). Başlık bu yüzden yanıt
/// yazılırken <b>yeniden</b> konur.</para>
///
/// <para><b>Neden <c>OnStarting</c> — <c>charset</c>:</b> gövdeyi yazan
/// <c>DefaultProblemDetailsWriter</c>, <c>WriteAsJsonAsync(..., contentType:
/// "application/problem+json")</c> çağırır ve bu çağrı <c>Content-Type</c>'ı <b>üzerine yazar</b>.
/// Yani başlığı önceden ayarlamak işe yaramaz. <c>OnStarting</c> geri çağırımı ise başlıklar
/// gerçekten gönderilmeden hemen önce çalışır ve son sözü söyler. RFC 7807 <c>charset</c>
/// zorunlu kılmaz ama JSON dışı bayt kaçışı yapılmadığı için (Almanca/Türkçe metinler doğrudan
/// UTF-8 taşınır) parametreyi açıkça bildirmek istemcideki mojibake riskini ortadan kaldırır.</para>
///
/// <para>İki çağrı yeri vardır ve ikisi de <b>etkisizdir (idempotent)</b>:
/// <c>ApiExceptionHandler</c> (istisnadan doğan yanıtlar) ve <c>AddProblemDetails</c> içindeki
/// <c>CustomizeProblemDetails</c> (framework'ün ürettiği 401/403/404 gibi yanıtlar).</para>
/// </summary>
internal static class ProblemDetailsResponseHeaders
{
    private const string ProblemJson = "application/problem+json";

    private const string ProblemJsonWithCharset = ProblemJson + "; charset=utf-8";

    /// <summary>
    /// İsteğin dilini <c>Content-Language</c> olarak yazar ve yanıt başlarken
    /// <c>Content-Type</c>'a <c>charset</c> ekler. Yanıt zaten başladıysa hiçbir şey yapılmaz
    /// (başlık değiştirmek <c>InvalidOperationException</c> fırlatırdı).
    /// </summary>
    public static void Apply(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var uiCulture = httpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture
                        ?? CultureInfo.CurrentUICulture;

        // Kültür adı boşsa (InvariantCulture) dil bildirmek yanıltıcı olurdu.
        if (!string.IsNullOrEmpty(uiCulture.Name))
        {
            httpContext.Response.Headers.ContentLanguage = uiCulture.Name;
        }

        httpContext.Response.OnStarting(EnsureCharsetCallback, httpContext);
    }

    private static readonly Func<object, Task> EnsureCharsetCallback = static state =>
    {
        var response = ((HttpContext)state).Response;
        var contentType = response.Headers[HeaderNames.ContentType].ToString();

        if (contentType.StartsWith(ProblemJson, StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("charset", StringComparison.OrdinalIgnoreCase))
        {
            response.Headers[HeaderNames.ContentType] = ProblemJsonWithCharset;
        }

        return Task.CompletedTask;
    };
}
