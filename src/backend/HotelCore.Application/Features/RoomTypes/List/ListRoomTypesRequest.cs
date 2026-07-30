using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.List;

/// <summary>
/// <c>GET /api/v1/room-types</c> — parametresizdir. Oda tipi sayısı az olduğu için sözleşme
/// gereği <b>sayfalama yoktur</b>, düz dizi döner (api-contracts.md → Rooms &amp; Housekeeping).
/// </summary>
public sealed record ListRoomTypesRequest : IRequest<IReadOnlyList<RoomTypeResponse>>;
