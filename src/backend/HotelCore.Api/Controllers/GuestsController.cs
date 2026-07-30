using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Guests.Common;
using HotelCore.Application.Features.Guests.Create;
using HotelCore.Application.Features.Guests.Delete;
using HotelCore.Application.Features.Guests.GetById;
using HotelCore.Application.Features.Guests.List;
using HotelCore.Application.Features.Guests.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Misafir uç noktaları (api-contracts-reservations.md → Guests).
/// <para>
/// İzin şeması bilinçlidir: misafir verisi rezervasyon modülünün parçasıdır, bu yüzden okuma
/// <c>Reservations.View</c>, yazma <c>Reservations.Create</c> gerektirir — resepsiyon rezervasyon
/// alırken misafir kaydını da açar, ayrı bir izin anahtarı tanımlanmaz (architecture.md §7'deki
/// izin listesi genişletilmez).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/guests")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class GuestsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sayfalı + aranabilir misafir listesi (soyad, ad sırasında).</summary>
    /// <remarks><c>search</c> ad, soyad ve e-postada büyük/küçük harf duyarsız arama yapar.</remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.ReservationsView)]
    [ProducesResponseType<PagedResult<GuestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<GuestResponse>> List(
        [FromQuery] ListGuestsRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Tek misafir — yanıtta geçmiş konaklama sayısı (<c>stayCount</c>) döner.</summary>
    /// <remarks>
    /// <c>stayCount</c> sunucuda hesaplanır (tamamlanmış, yani <c>CheckedOut</c> konaklamalar);
    /// entity'de kolon olarak tutulmaz. Liste yanıtında <c>null</c>'dır.
    /// </remarks>
    [HttpGet("{id:guid}", Name = nameof(GetGuestById))]
    [Authorize(Policy = Permissions.ReservationsView)]
    [ProducesResponseType<GuestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<GuestResponse> GetGuestById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetGuestByIdRequest(id), cancellationToken);

    /// <summary>Yeni misafir oluşturur.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.ReservationsCreate)]
    [ProducesResponseType<GuestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuestResponse>> Create(
        [FromBody] CreateGuestRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetGuestById), new { id = created.Id }, created);
    }

    /// <summary>Misafiri günceller.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ReservationsCreate)]
    [ProducesResponseType<GuestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<GuestResponse> Update(
        Guid id,
        [FromBody] UpdateGuestRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Kimlik route'tan gelir; gövdedeki bir "id" alanı dikkate alınmaz.
        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>
    /// Misafiri soft-delete eder; aktif veya gelecek tarihli rezervasyonu varsa <b>409</b>.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.ReservationsCreate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new DeleteGuestRequest(id), cancellationToken).ConfigureAwait(false);

        return NoContent();
    }
}
