using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;

namespace HotelCore.Application.Features.Hotels.GetById;

internal sealed class GetHotelByIdHandler(HotelReader reader)
    : IRequestHandler<GetHotelByIdRequest, HotelResponse>
{
    public Task<HotelResponse> Handle(GetHotelByIdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, cancellationToken);
    }
}
