using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RoomTypes.Common;
using HotelCore.Application.Features.RoomTypes.Create;
using HotelCore.Application.Features.RoomTypes.Delete;
using HotelCore.Application.Features.RoomTypes.GetById;
using HotelCore.Application.Features.RoomTypes.List;
using HotelCore.Application.Features.RoomTypes.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Oda tipi uç noktaları (api-contracts.md → Rooms &amp; Housekeeping).
/// <para>
/// Yetkilendirme <b>policy-based</b>'dir; policy'ler <c>Permissions.All</c> üzerinden Program.cs'te
/// otomatik üretilir, bu yüzden burada rol/izin adı hardcode edilmez, sabit kullanılır.
/// </para>
/// <para>
/// Ad/açıklama <c>Accept-Language</c>'e göre çözümlenir; çeviri yoksa kaydın varsayılan metni döner.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/room-types")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class RoomTypesController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Aktif otelin oda tipleri (düz dizi — sayfalama yoktur).</summary>
    /// <remarks>
    /// Yanıt <c>translations</c> alanını <b>içermez</b>; tüm diller yalnızca detay ucunda döner.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.RoomsView)]
    [ProducesResponseType<IReadOnlyList<RoomTypeResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<RoomTypeResponse>> List(CancellationToken cancellationToken) =>
        dispatcher.Send(new ListRoomTypesRequest(), cancellationToken);

    /// <summary>Tek oda tipi; düzenleme ekranı için tüm çevirileri de döner.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetRoomTypeById))]
    [Authorize(Policy = Permissions.RoomsView)]
    [ProducesResponseType<RoomTypeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RoomTypeResponse> GetRoomTypeById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetRoomTypeByIdRequest(id), cancellationToken);

    /// <summary>Yeni oda tipi oluşturur.</summary>
    /// <remarks>Kod otel içinde benzersizdir; çakışma <b>409</b> döner.</remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.RoomsManage)]
    [ProducesResponseType<RoomTypeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomTypeResponse>> Create(
        [FromBody] CreateRoomTypeRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetRoomTypeById), new { id = created.Id }, created);
    }

    /// <summary>Oda tipini günceller (çeviriler upsert edilir).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.RoomsManage)]
    [ProducesResponseType<RoomTypeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RoomTypeResponse> Update(
        Guid id,
        [FromBody] UpdateRoomTypeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>Oda tipini soft-delete eder; bağlı oda varsa <b>409</b> döner.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.RoomsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteRoomTypeRequest(id), cancellationToken).ConfigureAwait(false);

        return NoContent();
    }
}
