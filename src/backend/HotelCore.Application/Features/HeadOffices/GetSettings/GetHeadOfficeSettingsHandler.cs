using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.HeadOffices.Common;

namespace HotelCore.Application.Features.HeadOffices.GetSettings;

internal sealed class GetHeadOfficeSettingsHandler(HeadOfficeReader reader)
    : IRequestHandler<GetHeadOfficeSettingsRequest, HeadOfficeSettingsResponse>
{
    public Task<HeadOfficeSettingsResponse> Handle(
        GetHeadOfficeSettingsRequest request,
        CancellationToken cancellationToken) => reader.GetAsync(cancellationToken);
}
