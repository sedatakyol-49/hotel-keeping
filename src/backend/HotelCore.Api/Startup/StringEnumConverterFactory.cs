using System.Text.Json;
using HotelCore.Application.Common.Localization;
using System.Text.Json.Serialization;

namespace HotelCore.Api.Startup;

/// <summary>
/// Enum'ları JSON'da <b>adıyla</b> taşır (api-contracts.md: <c>"housekeepingStatus": "Dirty"</c>,
/// sayı değil) ve tanınmayan değerlerde <b>anlaşılır</b> bir hata üretir.
/// <para>
/// Neden <see cref="JsonStringEnumConverter"/> değil: varsayılan dönüştürücünün hata mesajı
/// .NET tip adını (<c>HotelCore.Domain.Enums...</c>) istemciye sızdırır ve izin verilen değerleri
/// söylemez. Buradaki dönüştürücü aynı OpenAPI şemasını üretir (Swashbuckle örnek bir değeri
/// serileştirip metin olduğunu gördüğü için <c>type: string</c> + <c>enum: [...]</c>) ama
/// hata mesajında yalnızca izin verilen değerleri listeler.
/// </para>
/// </summary>
internal sealed class StringEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        // Nullable<TEnum> burada yakalanmaz: System.Text.Json null kontrolünü kendisi yapıp
        // alttaki enum tipi için bu dönüştürücüyü kullanır.
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        var converterType = typeof(StringEnumConverter<>).MakeGenericType(typeToConvert);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    /// <summary>Tek bir enum tipi için metin ↔ enum dönüşümü.</summary>
    private sealed class StringEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly string AllowedValues = string.Join(", ", Enum.GetNames<TEnum>());

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType is not JsonTokenType.String)
            {
                throw new JsonException(Messages.EnumMustBeString(AllowedValues));
            }

            var raw = reader.GetString();

            // Sayısal kaçaklar (örn. "99") bilinçli olarak reddedilir: sözleşme enum ADI bekler.
            if (Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                return parsed;
            }

            throw new JsonException(Messages.EnumInvalidValue(raw, AllowedValues));
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteStringValue(value.ToString());
        }
    }
}
