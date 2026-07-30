using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Departments.Common;
using HotelCore.Application.Features.Departments.Create;
using HotelCore.Application.Features.Departments.Delete;
using HotelCore.Application.Features.Departments.List;
using HotelCore.Application.Features.Departments.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Departman uç noktaları (api-contracts.md → Personel).
/// <para>
/// Departman bir sınıflandırmadır ve geçmiş kayıt taşımaz; bu yüzden soft-delete edilmez,
/// silme <b>gerçek silmedir</b> ve bağlı çalışan varken 409 ile engellenir.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/departments")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class DepartmentsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Aktif otelin departmanları (düz dizi — sayfalama yoktur).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.EmployeesView)]
    [ProducesResponseType<IReadOnlyList<DepartmentResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<DepartmentResponse>> List(CancellationToken cancellationToken) =>
        dispatcher.Send(new ListDepartmentsRequest(), cancellationToken);

    /// <summary>Yeni departman oluşturur; ad otel içinde benzersizdir (409).</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    [ProducesResponseType<DepartmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentResponse>> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        // Departman detay ucu yoktur; liste ucu Location olarak yeterlidir.
        return Created($"/api/v1/departments/{created.Id}", created);
    }

    /// <summary>Departmanı günceller.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    [ProducesResponseType<DepartmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<DepartmentResponse> Update(
        Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>Departmanı siler; bağlı çalışan varsa <b>409</b> döner.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteDepartmentRequest(id), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }
}
