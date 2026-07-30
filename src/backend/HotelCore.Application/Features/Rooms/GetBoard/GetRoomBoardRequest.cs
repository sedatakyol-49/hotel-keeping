using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Features.Rooms.GetBoard;

/// <summary>
/// <c>GET /api/v1/rooms/board</c> — kat hizmetleri panosu. Parametresizdir; kapsam aktif oteldir.
/// İzin: <c>Housekeeping.View</c> (finansal alan içermez).
/// </summary>
public sealed record GetRoomBoardRequest : IRequest<RoomBoardResponse>;
