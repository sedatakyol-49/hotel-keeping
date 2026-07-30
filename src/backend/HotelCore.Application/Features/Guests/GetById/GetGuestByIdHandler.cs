using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Guests.Common;

namespace HotelCore.Application.Features.Guests.GetById;

internal sealed class GetGuestByIdHandler(GuestReader reader)
    : IRequestHandler<GetGuestByIdRequest, GuestResponse>
{
    public Task<GuestResponse> Handle(GetGuestByIdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetAsync(request.Id, cancellationToken);
    }
}
