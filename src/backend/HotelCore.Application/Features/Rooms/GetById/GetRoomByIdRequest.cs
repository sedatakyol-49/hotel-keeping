using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Features.Rooms.GetById;

/// <summary><c>GET /api/v1/rooms/{id}</c>.</summary>
/// <param name="Id">Oda kimliği.</param>
public sealed record GetRoomByIdRequest(Guid Id) : IRequest<RoomResponse>;
