using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;

namespace HotelCore.Application.Features.RatePlans.List;

internal sealed class ListRatePlansHandler(RatePlanReader reader)
    : IRequestHandler<ListRatePlansRequest, IReadOnlyList<RatePlanResponse>>
{
    public Task<IReadOnlyList<RatePlanResponse>> Handle(
        ListRatePlansRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.ListAsync(request.RoomTypeId, request.Date, cancellationToken);
    }
}
