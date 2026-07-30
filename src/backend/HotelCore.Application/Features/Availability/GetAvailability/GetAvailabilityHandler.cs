using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Availability.Common;

namespace HotelCore.Application.Features.Availability.GetAvailability;

internal sealed class GetAvailabilityHandler(AvailabilityReader reader)
    : IRequestHandler<GetAvailabilityRequest, AvailabilityResponse>
{
    public Task<AvailabilityResponse> Handle(
        GetAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAvailabilityAsync(request.From, request.To, request.RoomTypeId, cancellationToken);
    }
}
