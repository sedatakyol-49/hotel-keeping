using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Guests.Common;

namespace HotelCore.Application.Features.Guests.List;

internal sealed class ListGuestsHandler(GuestReader reader)
    : IRequestHandler<ListGuestsRequest, PagedResult<GuestResponse>>
{
    public Task<PagedResult<GuestResponse>> Handle(
        ListGuestsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.ListAsync(request.ToQuery(), cancellationToken);
    }
}
