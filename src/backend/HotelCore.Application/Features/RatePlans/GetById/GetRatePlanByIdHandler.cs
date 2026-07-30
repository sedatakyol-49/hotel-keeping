using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;

namespace HotelCore.Application.Features.RatePlans.GetById;

internal sealed class GetRatePlanByIdHandler(RatePlanReader reader)
    : IRequestHandler<GetRatePlanByIdRequest, RatePlanResponse>
{
    public Task<RatePlanResponse> Handle(GetRatePlanByIdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, cancellationToken);
    }
}
