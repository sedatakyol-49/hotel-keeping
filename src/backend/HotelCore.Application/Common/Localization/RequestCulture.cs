using System.Globalization;

namespace HotelCore.Application.Common.Localization;

/// <summary>
/// Aktif isteğin dili. <c>Accept-Language</c> başlığı <b>elle parse edilmez</b>: Api katmanındaki
/// <c>UseRequestLocalization</c> (bkz. <c>LocalizationConfiguration</c>) başlığı/cookie'yi/kullanıcı
/// profilini değerlendirip <see cref="CultureInfo.CurrentUICulture"/>'ı ayarlar; burada yalnızca
/// o değer okunur. Böylece culture çözüm sırası tek yerde tanımlı kalır.
/// </summary>
internal static class RequestCulture
{
    /// <summary>
    /// Çeviri araması için kullanılacak iki harfli dil kodu. Desteklenmeyen bir culture
    /// (veya invariant) durumunda varsayılan dile düşer.
    /// </summary>
    public static string Current
    {
        get
        {
            var culture = SupportedCultures.Normalize(CultureInfo.CurrentUICulture.Name);

            return SupportedCultures.IsSupported(culture) ? culture : SupportedCultures.Default;
        }
    }
}
