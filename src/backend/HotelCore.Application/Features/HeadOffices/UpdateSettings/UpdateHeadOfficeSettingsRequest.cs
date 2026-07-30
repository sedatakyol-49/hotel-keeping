using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.HeadOffices.Common;

namespace HotelCore.Application.Features.HeadOffices.UpdateSettings;

/// <summary>
/// <c>PUT /api/v1/head-office/settings</c> gövdesi. Hangi Head Office güncellenecegi
/// <b>gövdeden alınmaz</b>; kimlikteki <c>headOfficeId</c> claim'i kullanılır.
/// </summary>
public sealed record UpdateHeadOfficeSettingsRequest : IRequest<HeadOfficeSettingsResponse>
{
    public string BrandName { get; init; } = string.Empty;

    public string DefaultCulture { get; init; } = string.Empty;
}
