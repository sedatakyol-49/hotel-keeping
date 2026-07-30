using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.List;

internal sealed class ListReservationsHandler(ReservationReader reader)
    : IRequestHandler<ListReservationsRequest, PagedResult<ReservationResponse>>
{
    public Task<PagedResult<ReservationResponse>> Handle(
        ListReservationsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.ListAsync(request.ToQuery(), cancellationToken);
    }
}
