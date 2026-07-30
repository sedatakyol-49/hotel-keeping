using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Rooms.UpdateHousekeeping;

/// <summary>
/// <c>PATCH /api/v1/rooms/{id}/housekeeping</c> gövdesi:
/// <c>{ "status": "Inspected", "note": "Minibar dolduruldu" }</c>.
/// <para>
/// <see cref="IsOutOfOrder"/> alanı <b>yoktur</b>: bayrak <see cref="Status"/>'ten türetilir
/// (bkz. <c>HousekeepingState</c>). Bu uç <c>Housekeeping.Update</c> izniyle çalışır ve
/// finansal alan içermez.
/// </para>
/// </summary>
public sealed record UpdateHousekeepingRequest : IRequest<RoomResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }

    /// <summary>Yeni durum (<c>Clean | Dirty | Inspected | OutOfOrder</c>).</summary>
    public HousekeepingStatus Status { get; init; }

    /// <summary>Kat hizmetleri notu. Opsiyoneldir; <c>null</c> gönderilirse mevcut not temizlenir.</summary>
    public string? Note { get; init; }
}
