using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;

namespace HotelCore.Application.Features.RoomTypes.GetById;

/// <summary>
/// <c>GET /api/v1/room-types/{id}</c> — düzenleme ekranı için <b>tüm</b> çevirileri de döner.
/// </summary>
/// <param name="Id">Oda tipi kimliği.</param>
public sealed record GetRoomTypeByIdRequest(Guid Id) : IRequest<RoomTypeResponse>;
