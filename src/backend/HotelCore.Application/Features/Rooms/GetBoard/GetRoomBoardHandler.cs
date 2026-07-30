using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Features.Rooms.GetBoard;

/// <summary>
/// Kat bazlı pano + özet sayaçlar. Yanıt DTO'sunda hiçbir fiyat alanı bulunmaz ve sorgu bu
/// kolonları hiç okumaz (architecture.md §7 — Housekeeping rolü finansal veri görmez).
/// </summary>
internal sealed class GetRoomBoardHandler(RoomReader reader)
    : IRequestHandler<GetRoomBoardRequest, RoomBoardResponse>
{
    public Task<RoomBoardResponse> Handle(GetRoomBoardRequest request, CancellationToken cancellationToken) =>
        reader.GetBoardAsync(cancellationToken);
}
