using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.List;

internal sealed class ListTimeEntriesHandler(TimeEntryReader reader)
    : IRequestHandler<ListTimeEntriesRequest, PagedResult<TimeEntryResponse>>
{
    public Task<PagedResult<TimeEntryResponse>> Handle(
        ListTimeEntriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.ListAsync(request.ToQuery(), cancellationToken);
    }
}
