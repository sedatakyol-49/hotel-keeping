using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.RoomTypes.Delete;

/// <summary>
/// <c>DELETE /api/v1/room-types/{id}</c> — soft-delete. Bağlı oda varsa 409 döner.
/// </summary>
/// <param name="Id">Oda tipi kimliği.</param>
public sealed record DeleteRoomTypeRequest(Guid Id) : IRequest<Unit>;
