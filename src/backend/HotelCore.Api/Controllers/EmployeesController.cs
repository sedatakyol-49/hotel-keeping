using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Application.Features.Employees.Create;
using HotelCore.Application.Features.Employees.Delete;
using HotelCore.Application.Features.Employees.GetById;
using HotelCore.Application.Features.Employees.List;
using HotelCore.Application.Features.Employees.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Çalışan uç noktaları (api-contracts.md → Personel).
/// <para>
/// Silme <b>soft-delete</b>'tir: çalışanın izin ve zaman kayıtları korunur, kayıt yalnızca
/// listelerden düşer. Varsayılan liste görünümü aktif kadrodur; işten ayrılmışlar
/// <c>includeTerminated=true</c> ile bilinçli olarak istenir.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/employees")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class EmployeesController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sayfalı + filtreli çalışan listesi (soyad, ad sırasında).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.EmployeesView)]
    [ProducesResponseType<PagedResult<EmployeeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<EmployeeResponse>> List(
        [FromQuery] ListEmployeesRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Tek çalışan.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetEmployeeById))]
    [Authorize(Policy = Permissions.EmployeesView)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<EmployeeResponse> GetEmployeeById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetEmployeeByIdRequest(id), cancellationToken);

    /// <summary>Yeni çalışan oluşturur.</summary>
    /// <remarks>
    /// Personel numarası verilirse otel içinde benzersizdir (409); departman aynı otele ait
    /// olmalıdır, aksi hâlde 404.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeResponse>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetEmployeeById), new { id = created.Id }, created);
    }

    /// <summary>Çalışanı günceller.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<EmployeeResponse> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>Çalışanı soft-delete eder.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteEmployeeRequest(id), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }
}
