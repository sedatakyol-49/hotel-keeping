using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Availability.Common;

namespace HotelCore.Application.Features.Availability.GetOccupancy;

internal sealed class GetOccupancyHandler(AvailabilityReader reader)
    : IRequestHandler<GetOccupancyRequest, OccupancyResponse>
{
    public Task<OccupancyResponse> Handle(GetOccupancyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetOccupancyAsync(request.From, request.To, cancellationToken);
    }
}
