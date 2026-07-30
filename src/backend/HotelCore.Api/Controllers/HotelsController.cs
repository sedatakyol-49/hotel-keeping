using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Application.Features.Hotels.GetById;
using HotelCore.Application.Features.Hotels.List;
using HotelCore.Application.Features.Hotels.UpdateSettings;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Otel uç noktaları (api-contracts.md → Hotels &amp; Ayarlar).
/// <para>
/// <b>Erişim:</b> <c>Hotel</c> tenant-scoped değildir (tenant kökünün kendisidir), bu yüzden
/// global query filter onu süzmez; erişim <c>UserHotelAccess</c> tablosundan doğrulanır.
/// Erişilemeyen otel <b>404</b> döner — varlığı sızdırılmaz.
/// </para>
/// <para>
/// Bu uçlar <c>X-Hotel-Id</c> başlığına bağlı değildir: hangi otelin okunacağı/yazılacağı
/// route'taki kimlikten gelir. Böylece otel seçici, aktif otel seçilmeden de çalışır.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/hotels")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class HotelsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Kullanıcının erişebildiği oteller (düz dizi — sayfalama yoktur).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.HotelsView)]
    [ProducesResponseType<IReadOnlyList<HotelListItemResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<HotelListItemResponse>> List(CancellationToken cancellationToken) =>
        dispatcher.Send(new ListHotelsRequest(), cancellationToken);

    /// <summary>Otel künyesi ve vergi profili.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetHotelById))]
    [Authorize(Policy = Permissions.HotelsView)]
    [ProducesResponseType<HotelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<HotelResponse> GetHotelById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetHotelByIdRequest(id), cancellationToken);

    /// <summary>Otel ayarlarını (künye + vergi oranları) günceller.</summary>
    /// <remarks>
    /// Vergi oranları koda hardcode edilmez (architecture.md §4.1); faturalama bu değerleri okur.
    /// </remarks>
    [HttpPut("{id:guid}/settings")]
    [Authorize(Policy = Permissions.SettingsManage)]
    [ProducesResponseType<HotelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<HotelResponse> UpdateSettings(
        Guid id,
        [FromBody] UpdateHotelSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }
}
