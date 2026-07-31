namespace HotelCore.Application.Common.Localization;

/// <summary>
/// <c>Translation.EntityType</c> değerleri. Sabit tutulur çünkü satırlar bu metinle aranır;
/// entity'nin C# adı değişse bile veri bozulmasın diye <c>nameof</c> yerine sabit kullanılır.
/// </summary>
public static class TranslationEntityTypes
{
    public const string RoomType = "RoomType";

    /// <summary>Otel galerisi görselinin alt metni (misafir sitesi, WCAG 1.1.1).</summary>
    public const string HotelImage = "HotelImage";

    /// <summary>Oda tipi görselinin alt metni.</summary>
    public const string RoomTypeImage = "RoomTypeImage";

    /// <summary>Otelin misafir sitesinde gösterilen açıklaması.</summary>
    public const string Hotel = "Hotel";
}

/// <summary>
/// <c>Translation.Field</c> değerleri (çevrilen alan adları). Aynı gerekçeyle sabittir.
/// </summary>
public static class TranslationFields
{
    public const string Name = "Name";

    public const string Description = "Description";

    /// <summary>Görsellerin erişilebilirlik alt metni.</summary>
    public const string AltText = "AltText";
}
