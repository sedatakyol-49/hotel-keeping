using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.HeadOffices.Common;

namespace HotelCore.Application.Features.HeadOffices.UpdateSettings;

internal sealed class UpdateHeadOfficeSettingsHandler(
    IAppDbContext database,
    HeadOfficeReader reader)
    : IRequestHandler<UpdateHeadOfficeSettingsRequest, HeadOfficeSettingsResponse>
{
    public async Task<HeadOfficeSettingsResponse> Handle(
        UpdateHeadOfficeSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var headOffice = await reader.GetTrackedAsync(cancellationToken).ConfigureAwait(false);

        headOffice.BrandName = request.BrandName.Trim();
        headOffice.DefaultCulture = SupportedCultures.Normalize(request.DefaultCulture);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(cancellationToken).ConfigureAwait(false);
    }
}
