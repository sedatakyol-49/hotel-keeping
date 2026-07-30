using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.List;

internal sealed class ListVacationsHandler(VacationReader reader)
    : IRequestHandler<ListVacationsRequest, PagedResult<VacationRequestResponse>>
{
    public Task<PagedResult<VacationRequestResponse>> Handle(
        ListVacationsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.ListAsync(request.ToQuery(), cancellationToken);
    }
}
