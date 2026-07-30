namespace HotelCore.Application.Common.Localization;

/// <summary>
/// <c>Translation.EntityType</c> değerleri. Sabit tutulur çünkü satırlar bu metinle aranır;
/// entity'nin C# adı değişse bile veri bozulmasın diye <c>nameof</c> yerine sabit kullanılır.
/// </summary>
public static class TranslationEntityTypes
{
    public const string RoomType = "RoomType";
}

/// <summary>
/// <c>Translation.Field</c> değerleri (çevrilen alan adları). Aynı gerekçeyle sabittir.
/// </summary>
public static class TranslationFields
{
    public const string Name = "Name";

    public const string Description = "Description";
}
