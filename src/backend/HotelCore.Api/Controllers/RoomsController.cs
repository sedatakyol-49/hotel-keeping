using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Application.Features.Rooms.Create;
using HotelCore.Application.Features.Rooms.Delete;
using HotelCore.Application.Features.Rooms.GetBoard;
using HotelCore.Application.Features.Rooms.GetById;
using HotelCore.Application.Features.Rooms.List;
using HotelCore.Application.Features.Rooms.Update;
using HotelCore.Application.Features.Rooms.UpdateHousekeeping;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Oda ve kat hizmetleri uç noktaları (api-contracts.md → Rooms &amp; Housekeeping).
/// <para>
/// <b>Route sırası:</b> <c>GET rooms/board</c> bilinçli olarak <c>GET rooms/{id}</c>'den ÖNCE
/// tanımlanmıştır. Ayrıca <c>{id:guid}</c> kısıtı sayesinde "board" segmenti GUID olarak
/// yorumlanamaz — iki katmanlı koruma (attribute routing'de literal segment zaten parametreli
/// segmentten önceliklidir, ancak sıra okunabilirlik ve kazara kısıt kaldırılması için korunur).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/rooms")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class RoomsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sayfalı + filtreli oda listesi.</summary>
    /// <remarks>
    /// Sıralama: <c>floor</c>, sonra <c>number</c> (doğal/numerik: "2" &lt; "10" &lt; "201").
    /// <c>search</c> oda numarasında büyük/küçük harf duyarsız arama yapar.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.RoomsView)]
    [ProducesResponseType<PagedResult<RoomResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<RoomResponse>> List(
        [FromQuery] ListRoomsRequest request,
        CancellationToken cancellationToken) =>
        dispatcher.Send(request, cancellationToken);

    /// <summary>Kat hizmetleri panosu: kat bazlı gruplama + durum sayaçları.</summary>
    /// <remarks>
    /// <b>Finansal alan içermez</b> (architecture.md §7): Housekeeping rolü fiyat/ciro görmez.
    /// Bu yüzden yanıt DTO'sunda <c>basePrice</c>/<c>currency</c> tanımlı değildir.
    /// </remarks>
    [HttpGet("board")]
    [Authorize(Policy = Permissions.HousekeepingView)]
    [ProducesResponseType<RoomBoardResponse>(StatusCodes.Status200OK)]
    public Task<RoomBoardResponse> GetBoard(CancellationToken cancellationToken) =>
        dispatcher.Send(new GetRoomBoardRequest(), cancellationToken);

    /// <summary>Tek oda.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetRoomById))]
    [Authorize(Policy = Permissions.RoomsView)]
    [ProducesResponseType<RoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RoomResponse> GetRoomById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetRoomByIdRequest(id), cancellationToken);

    /// <summary>Yeni oda oluşturur.</summary>
    /// <remarks>
    /// Oda numarası otel içinde benzersizdir (çakışma <b>409</b>); <c>roomTypeId</c> aktif otele
    /// ait değilse <b>404</b> döner.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.RoomsManage)]
    [ProducesResponseType<RoomResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomResponse>> Create(
        [FromBody] CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetRoomById), new { id = created.Id }, created);
    }

    /// <summary>Odayı günceller.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.RoomsManage)]
    [ProducesResponseType<RoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<RoomResponse> Update(
        Guid id,
        [FromBody] UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>Odayı soft-delete eder; gelecek tarihli rezervasyon varsa <b>409</b> döner.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.RoomsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteRoomRequest(id), cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>Kat hizmetleri durumunu (ve notunu) günceller.</summary>
    /// <remarks>
    /// <c>isOutOfOrder</c> gövdede taşınmaz: durum <c>OutOfOrder</c> ise true, aksi hâlde false
    /// olacak şekilde tutarlı tutulur. <c>note</c> null/boş gönderilirse mevcut not temizlenir.
    /// </remarks>
    [HttpPatch("{id:guid}/housekeeping")]
    [Authorize(Policy = Permissions.HousekeepingUpdate)]
    [ProducesResponseType<RoomResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RoomResponse> UpdateHousekeeping(
        Guid id,
        [FromBody] UpdateHousekeepingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }
}
