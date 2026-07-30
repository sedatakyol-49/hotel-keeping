using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.GetById;

internal sealed class GetVacationByIdHandler(VacationReader reader)
    : IRequestHandler<GetVacationByIdRequest, VacationRequestResponse>
{
    public Task<VacationRequestResponse> Handle(
        GetVacationByIdRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, cancellationToken);
    }
}
