using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.GetFolio;

internal sealed class GetReservationFolioHandler(ReservationReader reader)
    : IRequestHandler<GetReservationFolioRequest, FolioResponse>
{
    public Task<FolioResponse> Handle(
        GetReservationFolioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetFolioAsync(request.Id, cancellationToken);
    }
}
