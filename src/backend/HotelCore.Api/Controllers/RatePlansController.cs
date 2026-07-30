using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;
using HotelCore.Application.Features.RatePlans.Create;
using HotelCore.Application.Features.RatePlans.Delete;
using HotelCore.Application.Features.RatePlans.GetById;
using HotelCore.Application.Features.RatePlans.List;
using HotelCore.Application.Features.RatePlans.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Fiyat planı uç noktaları (api-contracts-reservations.md → Rate Plans).
/// <para>
/// Okuma <c>Rates.View</c>, yazma <c>Rates.Manage</c> gerektirir (architecture.md §7).
/// Aynı <c>(roomTypeId, channel)</c> için tarih aralığı çakışan ikinci aktif plan
/// <b>409</b> ile reddedilir: bir gece için iki fiyat geçerli olamaz.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/rate-plans")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class RatePlansController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Fiyat planları — düz dizi (plan sayısı az olduğu için sayfalama yok).</summary>
    /// <remarks>
    /// <c>roomTypeId</c> ve <c>date</c> ile filtrelenir; <c>date</c> verilirse o gün geçerli
    /// (<c>validFrom &lt;= date &lt;= validTo</c>) planlar döner. Sıralama: <c>validFrom</c>, ad.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.RatesView)]
    [ProducesResponseType<IReadOnlyList<RatePlanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<RatePlanResponse>> List(
        [FromQuery] ListRatePlansRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Tek fiyat planı.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetRatePlanById))]
    [Authorize(Policy = Permissions.RatesView)]
    [ProducesResponseType<RatePlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RatePlanResponse> GetRatePlanById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetRatePlanByIdRequest(id), cancellationToken);

    /// <summary>Yeni fiyat planı oluşturur.</summary>
    /// <remarks>
    /// <c>roomTypeId</c> aktif otele ait olmalıdır (aksi hâlde <b>404</b>);
    /// tarih aralığı çakışması <b>409</b>.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.RatesManage)]
    [ProducesResponseType<RatePlanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RatePlanResponse>> Create(
        [FromBody] CreateRatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetRatePlanById), new { id = created.Id }, created);
    }

    /// <summary>Fiyat planını günceller (geçmiş rezervasyon tutarları etkilenmez).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.RatesManage)]
    [ProducesResponseType<RatePlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RatePlanResponse> Update(
        Guid id,
        [FromBody] UpdateRatePlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>
    /// Fiyat planını siler (gerçek silme). Plana bağlı rezervasyon varsa <b>409</b> —
    /// bu durumda plan <c>isActive = false</c> ile pasifleştirilir.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.RatesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteRatePlanRequest(id), cancellationToken).ConfigureAwait(false);

        return NoContent();
    }
}
