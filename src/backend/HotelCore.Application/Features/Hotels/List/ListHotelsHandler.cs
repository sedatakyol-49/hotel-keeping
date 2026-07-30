using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;

namespace HotelCore.Application.Features.Hotels.List;

internal sealed class ListHotelsHandler(HotelReader reader)
    : IRequestHandler<ListHotelsRequest, IReadOnlyList<HotelListItemResponse>>
{
    public Task<IReadOnlyList<HotelListItemResponse>> Handle(
        ListHotelsRequest request,
        CancellationToken cancellationToken) => reader.ListAsync(cancellationToken);
}
