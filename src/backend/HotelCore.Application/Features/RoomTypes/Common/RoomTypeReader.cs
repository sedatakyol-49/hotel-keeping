using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// Oda tipi yanıtlarının tek üretim noktası: liste, detay ve yazma uçlarının (Create/Update)
/// döndürdüğü gövde buradan gelir; böylece <c>currency</c>, <c>roomCount</c>, <c>amenities</c> ve
/// çeviri çözümlemesi tek yerde kalır.
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ı tarafından
/// uygulandığı için burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class RoomTypeReader(IAppDbContext database, IMapper mapper, TranslationService translations)
{
    /// <summary>Oda tipi listesi (koda göre sıralı). Sözleşme gereği <c>translations</c> içermez.</summary>
    public async Task<IReadOnlyList<RoomTypeResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await Project(database.RoomTypes.OrderBy(roomType => roomType.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            return [];
        }

        // Tüm oda tiplerinin aktif dildeki çevirileri TEK sorguda alınır (N+1 yok).
        var resolved = await translations
            .GetForCultureAsync(
                TranslationEntityTypes.RoomType,
                rows.ConvertAll(row => row.Id),
                RequestCulture.Current,
                cancellationToken)
            .ConfigureAwait(false);

        return rows.ConvertAll(row =>
            ToResponse(row, TranslationService.FieldsFor(resolved, row.Id), allTranslations: null));
    }

    /// <summary>
    /// Tek oda tipi. <paramref name="includeTranslations"/> true iken düzenleme ekranı için
    /// tüm diller <c>translations</c> alanında döner.
    /// </summary>
    public async Task<RoomTypeResponse> GetAsync(
        Guid id,
        bool includeTranslations,
        CancellationToken cancellationToken)
    {
        var row = await Project(database.RoomTypes.Where(roomType => roomType.Id == id))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(RoomType), id);

        var allCultures = await translations
            .GetAllCulturesAsync(TranslationEntityTypes.RoomType, id, cancellationToken)
            .ConfigureAwait(false);

        allCultures.TryGetValue(RequestCulture.Current, out var activeCultureFields);

        return ToResponse(
            row,
            activeCultureFields,
            includeTranslations ? BuildTranslationDtos(allCultures) : null);
    }

    /// <summary>
    /// Yalnızca gereken kolonları çeken izdüşüm. <c>Currency</c> oda tipinin bağlı olduğu otelden
    /// okunur (aktif otel modunda bu zaten aktif oteldir; Head Office konsolide modunda her satır
    /// kendi otelinin para birimini taşır). <c>RoomCount</c> ilişkili oda sayısıdır — Room üzerindeki
    /// global filter sayesinde silinmiş odalar sayılmaz.
    /// </summary>
    private static IQueryable<RoomTypeRow> Project(IQueryable<RoomType> query) =>
        query.Select(roomType => new RoomTypeRow(
            roomType.Id,
            roomType.Code,
            roomType.Name,
            roomType.Description,
            roomType.BasePrice,
            roomType.Hotel.Currency,
            roomType.Capacity,
            roomType.SizeSqm,
            roomType.Amenities,
            roomType.Rooms.Count));

    /// <summary>
    /// Satırı DTO'ya çevirir (Mapster) ve çok dilli alanları aktif dile göre çözümler;
    /// çeviri yoksa entity'deki varsayılan metne düşülür.
    /// </summary>
    private RoomTypeResponse ToResponse(
        RoomTypeRow row,
        Dictionary<string, string>? activeCultureFields,
        IReadOnlyDictionary<string, RoomTypeTranslationDto>? allTranslations)
    {
        var response = mapper.Map<RoomTypeResponse>(row);

        return response with
        {
            Name = TranslationService.Resolve(activeCultureFields, TranslationFields.Name, row.Name) ?? row.Name,
            Description = TranslationService.Resolve(
                activeCultureFields,
                TranslationFields.Description,
                row.Description),
            Translations = allTranslations
        };
    }

    private static Dictionary<string, RoomTypeTranslationDto> BuildTranslationDtos(
        Dictionary<string, Dictionary<string, string>> allCultures)
    {
        var result = new Dictionary<string, RoomTypeTranslationDto>(StringComparer.Ordinal);

        foreach (var (culture, fields) in allCultures)
        {
            result[culture] = new RoomTypeTranslationDto
            {
                Name = fields.GetValueOrDefault(TranslationFields.Name),
                Description = fields.GetValueOrDefault(TranslationFields.Description)
            };
        }

        return result;
    }
}
