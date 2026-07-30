using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Features.Rooms.GetById;

/// <summary>Tek odayı döner; başka otelin odası global filter yüzünden 404'tür.</summary>
internal sealed class GetRoomByIdHandler(RoomReader reader) : IRequestHandler<GetRoomByIdRequest, RoomResponse>
{
    public Task<RoomResponse> Handle(GetRoomByIdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, cancellationToken);
    }
}
