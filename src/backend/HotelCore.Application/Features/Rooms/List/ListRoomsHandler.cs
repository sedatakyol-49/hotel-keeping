using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Rooms.Common;

namespace HotelCore.Application.Features.Rooms.List;

/// <summary>
/// Sayfalı + filtreli oda listesi. Filtreleme, sıralama ve sayfalama tamamen
/// veritabanında yapılır (bkz. <c>RoomQueryExtensions.OrderByFloorThenNumber</c>).
/// </summary>
internal sealed class ListRoomsHandler(RoomReader reader)
    : IRequestHandler<ListRoomsRequest, PagedResult<RoomResponse>>
{
    public Task<PagedResult<RoomResponse>> Handle(ListRoomsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new RoomListQuery(
            request.ToPageQuery(),
            request.RoomTypeId,
            request.Floor,
            request.HousekeepingStatus,
            request.Search);

        return reader.ListAsync(query, cancellationToken);
    }
}
