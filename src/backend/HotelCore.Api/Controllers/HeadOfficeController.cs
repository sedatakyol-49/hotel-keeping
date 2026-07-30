using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.HeadOffices.Common;
using HotelCore.Application.Features.HeadOffices.GetSettings;
using HotelCore.Application.Features.HeadOffices.UpdateSettings;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Head Office (marka) ayarları — api-contracts.md → Hotels &amp; Ayarlar.
/// <para>
/// Hangi Head Office okunacağı/yazılacağı <b>kimlikten</b> gelir (JWT <c>headOfficeId</c>
/// claim'i); istekte kimlik taşınmaz, böylece başka markanın ayarlarına erişim yolu açılmaz.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/head-office")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class HeadOfficeController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Marka adı ve varsayılan dil.</summary>
    [HttpGet("settings")]
    [Authorize(Policy = Permissions.SettingsManage)]
    [ProducesResponseType<HeadOfficeSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<HeadOfficeSettingsResponse> GetSettings(CancellationToken cancellationToken) =>
        dispatcher.Send(new GetHeadOfficeSettingsRequest(), cancellationToken);

    /// <summary>Marka adını ve varsayılan dili günceller.</summary>
    /// <remarks>
    /// Müşteriye görünen marka adı koda hardcode edilmez; buradan yönetilir.
    /// </remarks>
    [HttpPut("settings")]
    [Authorize(Policy = Permissions.SettingsManage)]
    [ProducesResponseType<HeadOfficeSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<HeadOfficeSettingsResponse> UpdateSettings(
        [FromBody] UpdateHeadOfficeSettingsRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);
}
