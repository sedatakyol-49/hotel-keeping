using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace HotelCore.Api.Startup;

/// <summary>
/// İsteğin dilini <b>geçici olarak</b> yeniden yürürlüğe koyar.
/// <para>
/// <b>Neden gerekli:</b> <c>RequestLocalizationMiddleware</c> kültürü yalnızca kendi
/// <c>next()</c> çağrısı süresince ayarlar ve çıkarken iş parçacığının önceki kültürünü
/// <i>geri yükler</i>. Hata yanıtını yazan bileşenler (<c>UseExceptionHandler</c> →
/// <c>ApiExceptionHandler</c> ve <c>UseStatusCodePages</c>) boru hattında localization'dan
/// <b>daha dışarıda</b> olduğu için o noktada <see cref="CultureInfo.CurrentUICulture"/> artık
/// sunucunun kültürüdür. Bu da <c>title</c> alanının sunucu dilinde, <c>detail</c>/<c>errors</c>
/// alanlarının ise isteğin dilinde dönmesine — yani karışık dilli yanıta — yol açar.
/// </para>
/// <para>
/// Çözüm olarak boru hattı sırası değiştirilmez (localization'dan önce oluşan istisnalar da
/// ProblemDetails'e dönüşmeye devam etmelidir); bunun yerine yanıt yazılırken culture,
/// <see cref="IRequestCultureFeature"/>'dan (localization middleware'in bıraktığı sonuç)
/// okunup kısa süreliğine geri konur.
/// </para>
/// </summary>
internal static class RequestCultureScope
{
    /// <summary>
    /// İstek kültürünü uygular ve <c>Dispose</c> anında önceki kültürü geri yükler.
    /// Feature yoksa (istisna localization middleware'inden önce oluştuysa) hiçbir şey değişmez.
    /// </summary>
    public static IDisposable Apply(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var requestCulture = httpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture;

        return requestCulture is null ? NullScope.Instance : new CultureScope(requestCulture);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _previousUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(RequestCulture requestCulture)
        {
            CultureInfo.CurrentCulture = requestCulture.Culture;
            CultureInfo.CurrentUICulture = requestCulture.UICulture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
            // Kültür değiştirilmedi; geri alınacak bir şey yok.
        }
    }
}
