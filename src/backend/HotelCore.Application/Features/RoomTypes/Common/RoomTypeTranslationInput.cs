using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// Yazma uçlarındaki <c>translations</c> sözlüğünü <see cref="TranslationService"/>'in beklediği
/// <c>culture → (field → text?)</c> biçimine çevirir (tek yerde, Create ve Update için ortak).
/// <para>
/// Semantik (api-contracts.md → "Çeviri davranışı"):
/// <list type="bullet">
///   <item>bir dil için nesne gönderilmişse o dilin alanları <b>tam olarak</b> bu nesnedir:
///         gönderilmeyen/boş alan silinir,</item>
///   <item>dil değeri <c>null</c> ise o dilin tüm çevirileri silinir,</item>
///   <item>sözlükte hiç geçmeyen dil olduğu gibi korunur.</item>
/// </list>
/// </para>
/// </summary>
internal static class RoomTypeTranslationInput
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string?>> Empty =
        new(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> ToFieldValues(
        IReadOnlyDictionary<string, RoomTypeTranslationDto?>? translations)
    {
        if (translations is null || translations.Count == 0)
        {
            return Empty;
        }

        var result = new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal);

        foreach (var (culture, value) in translations)
        {
            result[SupportedCultures.Normalize(culture)] = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [TranslationFields.Name] = value?.Name,
                [TranslationFields.Description] = value?.Description
            };
        }

        return result;
    }
}
