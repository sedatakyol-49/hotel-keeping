using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Shifts.Common;
using HotelCore.Application.Features.Shifts.Create;
using HotelCore.Application.Features.Shifts.Delete;
using HotelCore.Application.Features.Shifts.GetPlan;
using HotelCore.Application.Features.Shifts.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Vardiya planı (Dienstplan) uç noktaları — api-contracts.md → HR.
/// <para>
/// Bir çalışana aynı gün için <b>tek</b> vardiya planlanır (<c>(EmployeeId, Date)</c> unique);
/// ikinci vardiya <b>409</b> döner. Plan gün × çalışan ızgarası olarak döner.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/shifts")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ShiftsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Vardiya planı: <c>?week=YYYY-Www</c> veya <c>?from=&amp;to=</c>.</summary>
    /// <remarks>
    /// İkisi birlikte gönderilirse <b><c>week</c> kazanır</b>; hiçbiri gönderilmezse geçerli ISO
    /// hafta (Pazartesi–Pazar) döner. Kullanılan aralık yanıttaki <c>from</c>/<c>to</c>/<c>week</c>
    /// alanlarında geri bildirilir.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.ShiftsView)]
    [ProducesResponseType<ShiftPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<ShiftPlanResponse> Plan(
        [FromQuery] GetShiftPlanRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Yeni vardiya planlar.</summary>
    /// <remarks>Aynı çalışan + aynı gün varsa <b>409</b>; çalışan aktif otelde değilse <b>404</b>.</remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.ShiftsEdit)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftResponse>> Create(
        [FromBody] CreateShiftRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        // Vardiya detay ucu yoktur; plan ucu (GET /shifts) yeterlidir.
        return Created($"/api/v1/shifts/{created.Id}", created);
    }

    /// <summary>Vardiyayı günceller (çalışan/gün değişebilir).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ShiftsEdit)]
    [ProducesResponseType<ShiftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ShiftResponse> Update(
        Guid id,
        [FromBody] UpdateShiftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>Vardiyayı siler (hard delete — plan satırının saklanması gerekmez).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.ShiftsEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteShiftRequest(id), cancellationToken).ConfigureAwait(false);

        return NoContent();
    }
}
