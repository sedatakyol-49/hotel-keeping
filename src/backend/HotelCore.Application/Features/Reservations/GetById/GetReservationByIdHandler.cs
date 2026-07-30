using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.GetById;

internal sealed class GetReservationByIdHandler(ReservationReader reader)
    : IRequestHandler<GetReservationByIdRequest, ReservationResponse>
{
    public Task<ReservationResponse> Handle(
        GetReservationByIdRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, cancellationToken);
    }
}
