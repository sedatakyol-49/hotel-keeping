namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// <c>RoomType.Amenities</c> alanının <b>tek</b> dönüşüm noktası: veritabanında virgülle ayrılmış
/// tek metin (<c>"wifi,minibar,balcony"</c>), API sözleşmesinde dizi (<c>["wifi",...]</c>).
/// Bölme/birleştirme kuralı başka hiçbir yerde tekrarlanmaz (okuma tarafında Mapster
/// konfigürasyonu, yazma tarafında handler ve validator buradan geçer).
/// </summary>
internal static class AmenityList
{
    /// <summary>Tek bir donanım anahtarı için üst sınır (toplam alan 500 karakterle sınırlı).</summary>
    public const int MaxItemLength = 50;

    /// <summary>Birleştirilmiş metnin veritabanı kolon sınırı.</summary>
    public const int MaxStoredLength = 500;

    /// <summary>Aynı anahtarın tekrarını önlemek için makul bir üst sınır.</summary>
    public const int MaxItemCount = 30;

    /// <summary>DB metnini API dizisine çevirir; boş/null değer boş dizi olur (null DEĞİL).</summary>
    public static IReadOnlyList<string> Parse(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? []
            : stored.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// API dizisini DB metnine çevirir: boşluklar kırpılır, boş öğeler atılır, tekrarlar
    /// (büyük/küçük harf duyarsız) elenir. Sonuç boşsa kolon <c>null</c> bırakılır.
    /// </summary>
    public static string? Format(IEnumerable<string>? amenities)
    {
        if (amenities is null)
        {
            return null;
        }

        var cleaned = amenities
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length == 0 ? null : string.Join(',', cleaned);
    }
}
