using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Common.Localization;

/// <summary>
/// Dinamik içerik çevirilerinin (architecture.md §4.6) tek erişim noktası:
/// <c>(EntityType, EntityId, Field, Culture) → Text</c>.
/// <para>
/// Okuma tarafında <b>N+1 sorgu üretmemek</b> için çeviriler entity listesi başına tek sorguda
/// toplanır. Yazma tarafında <see cref="UpsertAsync"/> yalnızca değişiklikleri <i>takibe ekler</i>;
/// <c>SaveChanges</c> çağrısı handler'a bırakılır ki entity ve çevirileri aynı transaction'da yazılsın.
/// </para>
/// <para>
/// <see cref="Translation"/> tenant-scoped ve soft-deletable DEĞİLDİR (satır zaten otele bağlı bir
/// entity'ye işaret eder); bu yüzden silme işlemi gerçek silmedir.
/// </para>
/// </summary>
internal sealed class TranslationService(IAppDbContext database)
{
    /// <summary>Boş sonuçlar için ayırma yapmamak adına paylaşılan sözlük.</summary>
    private static readonly Dictionary<string, string> EmptyFields = new(StringComparer.Ordinal);

    /// <summary>
    /// Verilen entity'ler için <paramref name="culture"/> dilindeki çevirileri döner:
    /// <c>entityId → (field → text)</c>. Çevirisi olmayan entity sözlükte bulunmaz —
    /// çağıran, entity üzerindeki varsayılan metne düşer (fallback).
    /// </summary>
    public async Task<Dictionary<Guid, Dictionary<string, string>>> GetForCultureAsync(
        string entityType,
        IReadOnlyCollection<Guid> entityIds,
        string culture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entityIds);

        var result = new Dictionary<Guid, Dictionary<string, string>>();
        if (entityIds.Count == 0)
        {
            return result;
        }

        // Sorgu (EntityType, EntityId) index'ini kullanır; kimlikler tek IN listesinde gider.
        var ids = entityIds.Distinct().ToArray();
        var rows = await database.Translations
            .Where(t => t.EntityType == entityType && t.Culture == culture && ids.Contains(t.EntityId))
            .Select(t => new { t.EntityId, t.Field, t.Text })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            if (!result.TryGetValue(row.EntityId, out var fields))
            {
                fields = new Dictionary<string, string>(StringComparer.Ordinal);
                result[row.EntityId] = fields;
            }

            fields[row.Field] = row.Text;
        }

        return result;
    }

    /// <summary>
    /// Tek entity'nin <b>tüm</b> dillerdeki çevirileri: <c>culture → (field → text)</c>.
    /// Düzenleme ekranı (GET /room-types/{id}) bu şekli kullanır.
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> GetAllCulturesAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var rows = await database.Translations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .Select(t => new { t.Culture, t.Field, t.Text })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var culture = SupportedCultures.Normalize(row.Culture);
            if (!result.TryGetValue(culture, out var fields))
            {
                fields = new Dictionary<string, string>(StringComparer.Ordinal);
                result[culture] = fields;
            }

            fields[row.Field] = row.Text;
        }

        return result;
    }

    /// <summary>
    /// Çeviri sözlüğünü upsert eder. Kural (api-contracts.md → "Çeviri davranışı"):
    /// <list type="bullet">
    ///   <item>metin doluysa satır eklenir/güncellenir,</item>
    ///   <item>metin <c>null</c>/boş gönderilmişse o dil-alan satırı <b>silinir</b>,</item>
    ///   <item>sözlükte hiç geçmeyen dil olduğu gibi korunur (upsert semantiği).</item>
    /// </list>
    /// <c>SaveChanges</c> ÇAĞIRMAZ.
    /// </summary>
    /// <param name="entityType">Çevrilen entity tipi (bkz. <see cref="TranslationEntityTypes"/>).</param>
    /// <param name="entityId">Çevrilen kaydın kimliği.</param>
    /// <param name="valuesByCulture"><c>culture → (field → text?)</c>; <c>null</c> metin = sil.</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public async Task UpsertAsync(
        string entityType,
        Guid entityId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> valuesByCulture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(valuesByCulture);

        if (valuesByCulture.Count == 0)
        {
            return;
        }

        var existing = await database.Translations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var (rawCulture, fields) in valuesByCulture)
        {
            var culture = SupportedCultures.Normalize(rawCulture);

            foreach (var (field, text) in fields)
            {
                var row = existing.Find(t =>
                    string.Equals(SupportedCultures.Normalize(t.Culture), culture, StringComparison.Ordinal)
                    && string.Equals(t.Field, field, StringComparison.Ordinal));

                if (string.IsNullOrWhiteSpace(text))
                {
                    if (row is not null)
                    {
                        database.Translations.Remove(row);
                        existing.Remove(row);
                    }

                    continue;
                }

                if (row is null)
                {
                    var added = new Translation
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        Field = field,
                        Culture = culture,
                        Text = text.Trim()
                    };

                    database.Translations.Add(added);
                    existing.Add(added);
                }
                else
                {
                    row.Text = text.Trim();
                }
            }
        }
    }

    /// <summary>
    /// Çevrilmiş metni döndürür; o dilde kayıt yoksa <paramref name="fallback"/> (entity'deki
    /// varsayılan değer) kullanılır.
    /// </summary>
    public static string? Resolve(Dictionary<string, string>? fields, string field, string? fallback)
    {
        if (fields is null)
        {
            return fallback;
        }

        return fields.TryGetValue(field, out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : fallback;
    }

    /// <summary>Çevirisi hiç olmayan entity'ler için boş sözlük (null kontrolünü sadeleştirir).</summary>
    public static Dictionary<string, string> FieldsFor(
        Dictionary<Guid, Dictionary<string, string>> translations,
        Guid entityId)
    {
        ArgumentNullException.ThrowIfNull(translations);

        return translations.TryGetValue(entityId, out var fields) ? fields : EmptyFields;
    }
}
