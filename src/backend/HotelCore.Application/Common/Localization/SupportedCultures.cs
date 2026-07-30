namespace HotelCore.Application.Common.Localization;

/// <summary>
/// Uygulamanın desteklediği diller (architecture.md §8). Api tarafındaki
/// <c>Localization:SupportedCultures</c> ayarıyla aynı kümedir; burada tutulmasının nedeni
/// <b>dinamik içerik çevirilerinin</b> (Translation tablosu) doğrulanmasının Application
/// katmanında yapılmasıdır — validator'lar HTTP yapılandırmasına bağımlı olmamalıdır.
/// </summary>
public static class SupportedCultures
{
    /// <summary>Varsayılan dil; çeviri bulunamadığında bu dilin/entity'nin metni kullanılır.</summary>
    public const string Default = "de";

    /// <summary>Desteklenen iki harfli dil kodları (küçük harf).</summary>
    public static IReadOnlyList<string> All { get; } = ["de", "en", "tr"];

    /// <summary>Yazma uçlarında gelen <c>translations</c> anahtarlarını doğrulamak için.</summary>
    public static bool IsSupported(string? culture) =>
        !string.IsNullOrWhiteSpace(culture)
        && All.Contains(Normalize(culture), StringComparer.Ordinal);

    /// <summary>Kültür kodunu karşılaştırma/saklama biçimine indirger ("DE-de" → "de").</summary>
    public static string Normalize(string culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var trimmed = culture.Trim();
        var separator = trimmed.IndexOf('-', StringComparison.Ordinal);

        return (separator > 0 ? trimmed[..separator] : trimmed).ToLowerInvariant();
    }
}
