using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.TimeEntries.ClockIn;
using HotelCore.Application.Features.TimeEntries.ClockOut;
using HotelCore.Application.Features.TimeEntries.Common;
using HotelCore.Application.Features.TimeEntries.Delete;
using HotelCore.Application.Features.TimeEntries.List;
using HotelCore.Application.Features.TimeEntries.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Zaman kaydı (Zeiterfassung) uç noktaları — api-contracts.md → HR.
/// <para>
/// Bir çalışanın <b>en fazla bir açık kaydı</b> olabilir (çıkışı yapılmamış mesai): ikinci
/// clock-in ve açık kayıt olmadan clock-out <b>409</b> döner. <c>workedMinutes</c> her zaman
/// sunucuda hesaplanır (mola düşülmüş); açık kayıtta null'dır.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/time-entries")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class TimeEntriesController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sayfalı + filtreli zaman kaydı listesi (en yeni mesai üstte).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.TimeTrackingView)]
    [ProducesResponseType<PagedResult<TimeEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<TimeEntryResponse>> List(
        [FromQuery] ListTimeEntriesRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Mesai girişi (clock-in).</summary>
    /// <remarks>
    /// Açık kayıt varsa <b>409</b>; gelecek tarihli giriş <b>400</b>; çalışan aktif otelde
    /// değilse <b>404</b>.
    /// </remarks>
    [HttpPost("clock-in")]
    [Authorize(Policy = Permissions.TimeTrackingRecord)]
    [ProducesResponseType<TimeEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimeEntryResponse>> ClockIn(
        [FromBody] ClockInRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        // Zaman kaydinin detay ucu yoktur; Location kaynagin kanonik yolunu isaret eder.
        return Created($"/api/v1/time-entries/{created.Id}", created);
    }

    /// <summary>Mesai çıkışı (clock-out) — çalışanın açık kaydını kapatır.</summary>
    /// <remarks>Açık kayıt yoksa <b>409</b>; çıkış girişten önceyse veya mola süreyi aşarsa <b>400</b>.</remarks>
    [HttpPost("clock-out")]
    [Authorize(Policy = Permissions.TimeTrackingRecord)]
    [ProducesResponseType<TimeEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<TimeEntryResponse> ClockOut(
        [FromBody] ClockOutRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Zaman kaydının manuel düzeltmesi (çalışan değiştirilemez).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.TimeTrackingRecord)]
    [ProducesResponseType<TimeEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<TimeEntryResponse> Update(
        Guid id,
        [FromBody] UpdateTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>Zaman kaydını siler (hard delete — saklama yükümlülüğü yoktur).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.TimeTrackingRecord)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteTimeEntryRequest(id), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }
}
