using HotelCore.Application.Common.Messaging;

namespace HotelCore.Application.Features.Rooms.Delete;

/// <summary>
/// <c>DELETE /api/v1/rooms/{id}</c> — soft-delete. Gelecek tarihli rezervasyon varsa 409.
/// </summary>
/// <param name="Id">Oda kimliği.</param>
public sealed record DeleteRoomRequest(Guid Id) : IRequest<Unit>;
