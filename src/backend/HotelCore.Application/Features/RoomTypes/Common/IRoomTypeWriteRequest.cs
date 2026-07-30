namespace HotelCore.Application.Features.RoomTypes.Common;

/// <summary>
/// Create ve Update isteklerinin paylaştığı gövde sözleşmesi. Doğrulama kuralları
/// (api-contracts.md → "Doğrulama kuralları") tek yerde — <see cref="RoomTypeWriteValidator{T}"/> —
/// tanımlanabilsin diye ayrıştırıldı.
/// </summary>
public interface IRoomTypeWriteRequest
{
    string Code { get; }

    string Name { get; }

    string? Description { get; }

    decimal BasePrice { get; }

    int Capacity { get; }

    int? SizeSqm { get; }

    IReadOnlyList<string>? Amenities { get; }

    IReadOnlyDictionary<string, RoomTypeTranslationDto?>? Translations { get; }
}
