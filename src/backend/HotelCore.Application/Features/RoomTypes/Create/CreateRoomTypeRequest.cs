using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.Create;

/// <summary>
/// <c>POST /api/v1/room-types</c> gövdesi. <c>currency</c> ve <c>roomCount</c> istekte
/// <b>bulunmaz</b>: ilki otelin ayarından, ikincisi bağlı odalardan hesaplanır.
/// </summary>
public sealed record CreateRoomTypeRequest : IRequest<RoomTypeResponse>, IRoomTypeWriteRequest
{
    /// <summary>Otel içinde benzersiz kısa kod (çakışma → 409).</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Varsayılan dildeki ad; dile özgü metinler <see cref="Translations"/> ile verilir.</summary>
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public decimal BasePrice { get; init; }

    public int Capacity { get; init; }

    public int? SizeSqm { get; init; }

    /// <summary>Donanım anahtarları (dizi); DB'de virgüllü metne çevrilir.</summary>
    public IReadOnlyList<string>? Amenities { get; init; }

    /// <summary>Opsiyonel çeviriler: <c>{ "de": { "name": "...", "description": "..." }, ... }</c>.</summary>
    public IReadOnlyDictionary<string, RoomTypeTranslationDto?>? Translations { get; init; }
}
