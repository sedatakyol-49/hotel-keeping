using System.Globalization;
using HotelCore.Application.Common.Security;
using Microsoft.AspNetCore.Localization;

namespace HotelCore.Api.Startup;

/// <summary>
/// i18n (architecture.md §8): varsayılan dil <c>de</c>, desteklenen diller <c>de/en/tr</c>.
/// Culture çözüm sırası: query string → cookie → <c>Accept-Language</c> → <b>kullanıcı profili
/// (<c>culture</c> claim'i)</b> → varsayılan.
/// </summary>
public static class LocalizationConfiguration
{
    public static RequestLocalizationOptions Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var defaultCulture = configuration["Localization:DefaultCulture"] ?? "de";
        var supported = configuration.GetSection("Localization:SupportedCultures").Get<string[]>()
                        ?? ["de", "en", "tr"];

        var cultures = Array.ConvertAll(supported, culture => new CultureInfo(culture));

        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(defaultCulture),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
            ApplyCurrentCultureToResponseHeaders = true
        };

        // Standart sağlayıcılardan sonra çalışır: Accept-Language yoksa kullanıcının profili kullanılır.
        options.RequestCultureProviders.Add(new CustomRequestCultureProvider(context =>
        {
            var culture = context.User.FindFirst(JwtClaimNames.Culture)?.Value;

            return Task.FromResult<ProviderCultureResult?>(
                string.IsNullOrWhiteSpace(culture) ? null : new ProviderCultureResult(culture));
        }));

        return options;
    }
}
