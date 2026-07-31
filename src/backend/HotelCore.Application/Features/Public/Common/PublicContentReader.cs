using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.RoomTypes.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>Katalog okuma satırı — <b>GUID yanıta yazılmaz</b>, yalnızca sunucu içi sorgular için.</summary>
internal sealed record PublicRoomTypeRow(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal BasePrice,
    int Capacity,
    int? SizeSqm,
    string? Amenities);

/// <summary>
/// Misafir sitesinin <b>içerik</b> okuma yolu: oda tipleri, görseller ve çok dilli metinler.
///
/// <para><b>Çeviri davranışı</b> mevcut kuralla aynıdır (architecture.md §4.6): metin
/// <c>Translation</c> tablosundan <c>(EntityType, EntityId, Field, Culture)</c> ile çözülür,
/// o dilde kayıt yoksa entity üzerindeki varsayılan metne düşülür. Çeviriler entity listesi
/// başına <b>tek</b> sorguda toplanır (N+1 yok).</para>
///
/// <para><b>Tenant izolasyonu elle yazılmaz:</b> <c>RoomType</c>, <c>RoomTypeImage</c> ve
/// <c>HotelImage</c> tenant-scoped'dır; global query filter aktif otelin dışındaki satırları
/// zaten süzer. Bu yüzden burada <c>HotelId</c> koşulu <b>yoktur</b> ve
/// <c>IgnoreQueryFilters()</c> <b>kullanılmaz</b> — yanlış otelin içeriği fiziksel olarak
/// görünmez.</para>
/// </summary>
internal sealed class PublicContentReader(IAppDbContext database, TranslationService translations)
{
    /// <summary>Katalog kartındaki kısa açıklamanın üst sınırı.</summary>
    private const int ShortDescriptionLength = 180;

    /// <summary>
    /// Otelin oda tipleri, isteğin dilinde. Sıralama <c>Code</c>'a göre <b>deterministiktir</b>:
    /// prerender edilen sayfaların sırası her derlemede aynı olmalıdır.
    /// </summary>
    public async Task<IReadOnlyList<PublicRoomTypeRow>> ListRoomTypesAsync(
        string culture,
        CancellationToken cancellationToken)
    {
        var rows = await database.RoomTypes
            .AsNoTracking()
            .OrderBy(roomType => roomType.Code)
            .Select(roomType => new PublicRoomTypeRow(
                roomType.Id,
                roomType.Code,
                roomType.Name,
                roomType.Description,
                roomType.BasePrice,
                roomType.Capacity,
                roomType.SizeSqm,
                roomType.Amenities))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await ApplyTranslationsAsync(rows, culture, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tek oda tipi; kod <b>büyük/küçük harf duyarsızdır</b> (<c>dbl</c> = <c>DBL</c>).
    /// Bulunamazsa <c>null</c> — çağıran <c>404 ROOM_TYPE_NOT_FOUND</c> üretir.
    /// </summary>
    public async Task<PublicRoomTypeRow?> FindRoomTypeAsync(
        string code,
        string culture,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();

        // CA1304/CA1311/CA1862 bastırılır: bu ifade .NET'te değil VERİTABANINDA çalışır
        // (`upper("Code") = @p`), dolayısıyla "current culture" diye bir şey yoktur ve
        // StringComparison alan aşırı yüklemeleri EF Core tarafından çevrilemez. Kod alfabesi
        // ASCII'dir (SGL/DBL/SUI), bu yüzden Türkçe "i" sorunu da doğmaz.
#pragma warning disable CA1304, CA1311, CA1862
        var row = await database.RoomTypes
            .AsNoTracking()
            .Where(roomType => roomType.Code.ToUpper() == normalized)
#pragma warning restore CA1304, CA1311, CA1862
            .Select(roomType => new PublicRoomTypeRow(
                roomType.Id,
                roomType.Code,
                roomType.Name,
                roomType.Description,
                roomType.BasePrice,
                roomType.Capacity,
                roomType.SizeSqm,
                roomType.Amenities))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var translated = await ApplyTranslationsAsync([row], culture, cancellationToken).ConfigureAwait(false);

        return translated[0];
    }

    /// <summary>Oda tipi görselleri: <c>roomTypeId → görseller</c> (sıraya göre).</summary>
    public async Task<Dictionary<Guid, List<PublicImageResponse>>> GetRoomTypeImagesAsync(
        IReadOnlyCollection<Guid> roomTypeIds,
        string culture,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<PublicImageResponse>>();
        if (roomTypeIds.Count == 0)
        {
            return result;
        }

        var ids = roomTypeIds.Distinct().ToArray();

        var images = await database.RoomTypeImages
            .AsNoTracking()
            .Where(image => ids.Contains(image.RoomTypeId))
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .Select(image => new
            {
                image.Id,
                image.RoomTypeId,
                image.Url,
                image.AltText,
                image.Width,
                image.Height,
                image.SortOrder
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var altTexts = await translations
            .GetForCultureAsync(
                TranslationEntityTypes.RoomTypeImage,
                images.ConvertAll(image => image.Id),
                culture,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var image in images)
        {
            if (!result.TryGetValue(image.RoomTypeId, out var list))
            {
                list = [];
                result[image.RoomTypeId] = list;
            }

            list.Add(new PublicImageResponse
            {
                Url = image.Url,
                Alt = TranslationService.Resolve(
                    TranslationService.FieldsFor(altTexts, image.Id),
                    TranslationFields.AltText,
                    image.AltText),
                Width = image.Width,
                Height = image.Height,
                SortOrder = image.SortOrder
            });
        }

        return result;
    }

    /// <summary>Otel galerisi (sıraya göre); ilk öğe marka listesindeki kapak görselidir.</summary>
    public async Task<List<PublicImageResponse>> GetHotelImagesAsync(
        string culture,
        CancellationToken cancellationToken)
    {
        var images = await database.HotelImages
            .AsNoTracking()
            .OrderBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .Select(image => new
            {
                image.Id,
                image.Url,
                image.AltText,
                image.Width,
                image.Height,
                image.SortOrder
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var altTexts = await translations
            .GetForCultureAsync(
                TranslationEntityTypes.HotelImage,
                images.ConvertAll(image => image.Id),
                culture,
                cancellationToken)
            .ConfigureAwait(false);

        return images.ConvertAll(image => new PublicImageResponse
        {
            Url = image.Url,
            Alt = TranslationService.Resolve(
                TranslationService.FieldsFor(altTexts, image.Id),
                TranslationFields.AltText,
                image.AltText),
            Width = image.Width,
            Height = image.Height,
            SortOrder = image.SortOrder
        });
    }

    /// <summary>
    /// Otelin misafir sitesindeki açıklaması.
    /// <para>
    /// <b>Şema notu:</b> <c>Hotel</c> tablosunda açıklama kolonu <b>yoktur</b> (sözleşme §2.2
    /// <c>description</c> alanını ister). Metin bu yüzden yalnızca <c>Translation</c> tablosundan
    /// (<c>EntityType = "Hotel"</c>, <c>Field = "Description"</c>) okunur; kayıt yoksa
    /// <c>null</c> döner. Belge–şema çelişkisi olarak raporlanmıştır.
    /// </para>
    /// </summary>
    public async Task<string?> GetHotelDescriptionAsync(
        Guid hotelId,
        string culture,
        CancellationToken cancellationToken)
    {
        var fields = await translations
            .GetForCultureAsync(TranslationEntityTypes.Hotel, [hotelId], culture, cancellationToken)
            .ConfigureAwait(false);

        var resolved = TranslationService.Resolve(
            TranslationService.FieldsFor(fields, hotelId),
            TranslationFields.Description,
            fallback: null);

        if (resolved is not null)
        {
            return resolved;
        }

        // Çeviri yoksa otelin varsayılan diline düşülür (mevcut fallback kuralı).
        var defaults = await translations
            .GetForCultureAsync(
                TranslationEntityTypes.Hotel,
                [hotelId],
                SupportedCultures.Default,
                cancellationToken)
            .ConfigureAwait(false);

        return TranslationService.Resolve(
            TranslationService.FieldsFor(defaults, hotelId),
            TranslationFields.Description,
            fallback: null);
    }

    /// <summary>Donanım anahtarlarını CSV'den diziye çevirir — dönüşüm kuralı tek yerdedir.</summary>
    public static IReadOnlyList<string> Amenities(string? stored) => AmenityList.Parse(stored);

    /// <summary>
    /// Katalog kartı için kısa açıklama.
    /// <para>
    /// <b>Şema notu:</b> ayrı bir "kısa açıklama" kolonu yoktur; sözleşme hem
    /// <c>shortDescription</c> hem <c>description</c> ister. Kısa metin uzun metinden
    /// <b>cümle sınırında</b> türetilir — kelimeyi ortadan kesmek katalog kartında okunaksız bir
    /// metin üretirdi. Belge–şema çelişkisi olarak raporlanmıştır.
    /// </para>
    /// </summary>
    public static string? ShortDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var text = description.Trim();
        if (text.Length <= ShortDescriptionLength)
        {
            return text;
        }

        var window = text[..ShortDescriptionLength];
        var sentenceEnd = window.LastIndexOfAny(['.', '!', '?']);
        if (sentenceEnd > 40)
        {
            return window[..(sentenceEnd + 1)];
        }

        var wordEnd = window.LastIndexOf(' ');

        return (wordEnd > 40 ? window[..wordEnd] : window).TrimEnd() + "…";
    }

    private async Task<IReadOnlyList<PublicRoomTypeRow>> ApplyTranslationsAsync(
        List<PublicRoomTypeRow> rows,
        string culture,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var byCulture = await translations
            .GetForCultureAsync(
                TranslationEntityTypes.RoomType,
                rows.ConvertAll(row => row.Id),
                culture,
                cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row =>
        {
            var fields = TranslationService.FieldsFor(byCulture, row.Id);

            return row with
            {
                Name = TranslationService.Resolve(fields, TranslationFields.Name, row.Name) ?? row.Name,
                Description = TranslationService.Resolve(
                    fields,
                    TranslationFields.Description,
                    row.Description)
            };
        });
    }
}
