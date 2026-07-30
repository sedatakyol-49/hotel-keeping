using System.Text.Json.Serialization;

namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// Oda tipinin çok dilli alanları (api-contracts.md → "Çeviri davranışı"). Bir alan
/// <c>null</c> gönderilirse o dildeki çeviri <b>silinir</b>.
/// </summary>
public sealed record RoomTypeTranslationDto
{
    public string? Name { get; init; }

    public string? Description { get; init; }
}

/// <summary>
/// <c>RoomTypeResponse</c> — api-contracts.md → "Şekiller" ile birebir.
/// <para>
/// <see cref="Name"/> ve <see cref="Description"/> <b>aktif dile göre çözümlenmiş</b> metinlerdir;
/// o dilde çeviri yoksa entity üzerindeki varsayılan değere düşülür.
/// </para>
/// <para>
/// <see cref="Translations"/> yalnızca <c>GET /room-types/{id}</c> (düzenleme ekranı) yanıtında
/// doldurulur; liste yanıtında alan JSON'a hiç yazılmaz (sözleşme: "liste yanıtında dönmez").
/// </para>
/// </summary>
public sealed record RoomTypeResponse
{
    public Guid Id { get; init; }

    /// <summary>Otel içinde benzersiz kısa kod (DBL, SGL, SUI).</summary>
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public decimal BasePrice { get; init; }

    /// <summary>Oda tipinin bağlı olduğu otelin ISO 4217 para birimi (<c>Hotel.Currency</c>).</summary>
    public string Currency { get; init; } = string.Empty;

    public int Capacity { get; init; }

    public int? SizeSqm { get; init; }

    /// <summary>Donanım anahtarları — DB'de virgüllü metin, API'de dizi (bkz. <c>AmenityList</c>).</summary>
    public IReadOnlyList<string> Amenities { get; init; } = [];

    /// <summary>Bu tipe bağlı silinmemiş oda sayısı.</summary>
    public int RoomCount { get; init; }

    /// <summary>Tüm diller (<c>culture → { name, description }</c>). Liste yanıtında bulunmaz.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, RoomTypeTranslationDto>? Translations { get; init; }
}
