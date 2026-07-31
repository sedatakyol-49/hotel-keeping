using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace HotelCore.Application.Features.Public.Common;

/// <summary>
/// Saat (<c>TimeOnly</c>) alanlarının sözleşmedeki biçimi: <c>HH:mm</c>.
/// <para>
/// <b>Neden özel dönüştürücü:</b> System.Text.Json varsayılanı saniyeyi de yazar
/// (<c>15:00:00</c>), sözleşme ise <c>"15:00"</c> der. Fark kozmetik değildir —
/// <c>orderSummary.hash</c> kanonik JSON üzerinden hesaplandığı için biçim değişikliği
/// doğrudan hash'i değiştirir ve istemciyle sunucu asla uzlaşamaz.
/// </para>
/// </summary>
public sealed class PublicTimeOnlyConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH\\:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();

        return TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)
            ? exact
            : TimeOnly.Parse(raw!, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}

/// <summary>Nullable saat alanları (<c>estimatedArrivalLocalTime</c>).</summary>
public sealed class PublicNullableTimeOnlyConverter : JsonConverter<TimeOnly?>
{
    private static readonly PublicTimeOnlyConverter Inner = new();

    public override TimeOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType is JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(TimeOnly), options);

    public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}

/// <summary>
/// Public kanalın JSON kuralları: <b>anlık görüntülerin</b> (snapshot) serileştirilmesi ve
/// <c>orderSummary.hash</c>'in kanonik hesabı.
/// </summary>
internal static class PublicJson
{
    /// <summary>
    /// Anlık görüntü seçenekleri. HTTP yanıt seçeneklerinden <b>bağımsız</b> ve sabittir:
    /// dondurulmuş bir kanıt kaydı, hostun JSON yapılandırması değiştiğinde okunamaz hâle
    /// gelmemelidir.
    /// </summary>
    public static readonly JsonSerializerOptions Snapshot = CreateSnapshotOptions();

    private static JsonSerializerOptions CreateSnapshotOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false
        };

        options.Converters.Add(new PublicTimeOnlyConverter());
        options.Converters.Add(new PublicNullableTimeOnlyConverter());

        return options;
    }

    /// <summary>Nesneyi anlık görüntü JSON'una çevirir.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Snapshot);

    /// <summary>Anlık görüntüyü geri okur; bozuk/boş kayıtta <c>null</c> döner.</summary>
    public static T? Deserialize<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Snapshot);
        }
        catch (JsonException)
        {
            // Anlık görüntü okunamıyorsa çağıran yeniden hesaplamaya düşer; istek 500 ile ölmez.
            return null;
        }
    }

    /// <summary>
    /// <c>orderSummary.hash</c>: nesnenin <b><c>hash</c> alanı hariç</b>, anahtarları
    /// <b>ordinal sıralı</b>, boşluksuz, <c>InvariantCulture</c> sayı biçimli kanonik JSON'unun
    /// SHA-256'sı; <c>sha256:</c> öneki ve <b>küçük harf hex</b>.
    /// <para>
    /// <b>Neden kanonikleştirme şart:</b> sunucu ile istemci aynı nesneyi farklı anahtar
    /// sırasıyla veya farklı boşlukla serileştirirse hash'ler tutmaz ve her rezervasyon
    /// <c>409 SUMMARY_CHANGED</c> alırdı. Sayı biçimi de kanonun parçasıdır: <c>468.00</c> ile
    /// <c>468</c> farklı bayt dizileridir, bu yüzden <c>decimal</c> ölçeği <b>korunur</b>
    /// (System.Text.Json <c>decimal</c>'i ham metin olarak yazar).
    /// </para>
    /// </summary>
    public static string ComputeSummaryHash(PublicOrderSummaryResponse summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        // Hash alanı hesabın dışındadır: kendisini içeren bir özet hesaplanamaz.
        var node = JsonNode.Parse(JsonSerializer.Serialize(summary with { Hash = string.Empty }, Snapshot))!;
        node.AsObject().Remove("hash");

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(node, writer);
        }

        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    /// <summary>Anahtarları ordinal sıralayarak yazar; dizilerin sırası <b>korunur</b> (anlamlıdır).</summary>
    private static void WriteCanonical(JsonNode? node, Utf8JsonWriter writer)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                break;

            case JsonObject jsonObject:
                writer.WriteStartObject();
                foreach (var property in jsonObject.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonArray jsonArray:
                writer.WriteStartArray();
                foreach (var item in jsonArray)
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;

            default:
                // JsonValue: ham JSON metnini olduğu gibi yazar (sayı ölçeği korunur).
                node.WriteTo(writer);
                break;
        }
    }
}
