using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Vacations.Approve;
using HotelCore.Application.Features.Vacations.Balances;
using HotelCore.Application.Features.Vacations.Cancel;
using HotelCore.Application.Features.Vacations.Common;
using HotelCore.Application.Features.Vacations.Create;
using HotelCore.Application.Features.Vacations.GetById;
using HotelCore.Application.Features.Vacations.List;
using HotelCore.Application.Features.Vacations.Reject;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// İzin (Urlaub) uç noktaları — api-contracts.md → HR.
/// <para>
/// Bakiye (<c>VacationBalance.UsedDays</c>) yalnızca <b>onay</b> ile artar ve onaylı talebin
/// <b>iptali</b> ile geri düşer; durum değişikliği ile bakiye düzeltmesi aynı transaction'da
/// yazılır (architecture.md §5).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/vacations")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class VacationsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sayfalı + filtreli izin talebi listesi (en yeni üstte).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.VacationsView)]
    [ProducesResponseType<PagedResult<VacationRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<VacationRequestResponse>> List(
        [FromQuery] ListVacationsRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Yıl bazında izin bakiyeleri (düz dizi — sayfalama yoktur).</summary>
    /// <remarks>
    /// Bakiye satırı henüz oluşmamış çalışan için değerler <c>annualLeaveDays</c>'ten türetilir;
    /// bu uç <b>veri yazmaz</b>.
    /// </remarks>
    [HttpGet("balances")]
    [Authorize(Policy = Permissions.VacationsView)]
    [ProducesResponseType<IReadOnlyList<VacationBalanceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<VacationBalanceResponse>> Balances(
        [FromQuery] ListVacationBalancesRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Tek izin talebi.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetVacationById))]
    [Authorize(Policy = Permissions.VacationsView)]
    [ProducesResponseType<VacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<VacationRequestResponse> GetVacationById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetVacationByIdRequest(id), cancellationToken);

    /// <summary>Yeni izin talebi oluşturur (durum <c>Pending</c>).</summary>
    /// <remarks>
    /// Aynı çalışan için tarih aralığı çakışan bekleyen/onaylı talep varsa <b>409</b>; çalışan
    /// aktif otelde değilse <b>404</b>. Bakiye bu adımda değişmez.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.VacationsRequest)]
    [ProducesResponseType<VacationRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VacationRequestResponse>> Create(
        [FromBody] CreateVacationRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetVacationById), new { id = created.Id }, created);
    }

    /// <summary>Talebi onaylar; ilgili yılın <c>usedDays</c> değeri artar.</summary>
    /// <remarks>Yalnızca <c>Pending</c> talep onaylanabilir; karara bağlanmış talep <b>409</b>.</remarks>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Permissions.VacationsApprove)]
    [ProducesResponseType<VacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<VacationRequestResponse> Approve(
        Guid id,
        [FromBody] ApproveVacationRequest? request,
        CancellationToken cancellationToken)
    {
        // Govde opsiyoneldir (yalnizca not tasir); kimlik route'tan gelir.
        var command = (request ?? new ApproveVacationRequest()) with { Id = id };

        return dispatcher.Send(command, cancellationToken);
    }

    /// <summary>Talebi reddeder (bakiye değişmez).</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Permissions.VacationsApprove)]
    [ProducesResponseType<VacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<VacationRequestResponse> Reject(
        Guid id,
        [FromBody] RejectVacationRequest? request,
        CancellationToken cancellationToken)
    {
        var command = (request ?? new RejectVacationRequest()) with { Id = id };

        return dispatcher.Send(command, cancellationToken);
    }

    /// <summary>Talebi iptal eder; onaylı talepte <c>usedDays</c> geri düşer.</summary>
    /// <remarks>
    /// Yetki iki alternatiflidir: <c>Vacations.Approve</c> (her talep) <b>veya</b>
    /// <c>Vacations.Request</c> (yalnızca kendi talebi). Tek policy ile ifade edilemediği için
    /// burada yalnızca kimlik doğrulaması istenir, izin/sahiplik kontrolü handler'da yapılır ve
    /// yetkisiz istek <b>403</b> döner.
    /// </remarks>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<VacationRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<VacationRequestResponse> Cancel(
        Guid id,
        [FromBody] CancelVacationRequest? request,
        CancellationToken cancellationToken)
    {
        var command = (request ?? new CancelVacationRequest()) with { Id = id };

        return dispatcher.Send(command, cancellationToken);
    }
}
