using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Reservations.Cancel;
using HotelCore.Application.Features.Reservations.CheckIn;
using HotelCore.Application.Features.Reservations.CheckOut;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Application.Features.Reservations.Create;
using HotelCore.Application.Features.Reservations.GetById;
using HotelCore.Application.Features.Reservations.GetFolio;
using HotelCore.Application.Features.Reservations.List;
using HotelCore.Application.Features.Reservations.NoShow;
using HotelCore.Application.Features.Reservations.Update;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Rezervasyon uç noktaları (api-contracts-reservations.md → Reservations).
/// <para>
/// <b>Tutar hiçbir uçta istemciden alınmaz:</b> <c>totalAmount</c> geçerli fiyat planı ya da oda
/// tipinin <c>basePrice</c>'ı üzerinden sunucuda hesaplanır (POST ve PUT aynı hesabı kullanır).
/// </para>
/// <para>
/// <b>Durum geçişleri</b> yalnızca aksiyon uçlarıyla yapılır (check-in / check-out / cancel /
/// no-show) ve kural tek bir yardımcıda toplanmıştır (<c>ReservationStatusMachine</c>);
/// geçersiz geçiş <b>409</b> döner ve mesaj hangi geçişin denendiğini söyler.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/reservations")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ReservationsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Sayfalı + filtreli rezervasyon listesi (giriş tarihi sırasında).</summary>
    /// <remarks>
    /// Filtreler: <c>status</c>, <c>channel</c>, <c>roomId</c>, <c>guestId</c>, <c>from</c>,
    /// <c>to</c>, <c>search</c>. <c>from</c>/<c>to</c> birlikte verildiğinde aralıkla
    /// <b>kesişen</b> konaklamalar döner (yarı açık aralık). <c>search</c> rezervasyon
    /// numarasında ve misafir ad/soyadında arama yapar.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.ReservationsView)]
    [ProducesResponseType<PagedResult<ReservationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<ReservationResponse>> List(
        [FromQuery] ListReservationsRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Tek rezervasyon (detay çekmecesi).</summary>
    [HttpGet("{id:guid}", Name = nameof(GetReservationById))]
    [Authorize(Policy = Permissions.ReservationsView)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ReservationResponse> GetReservationById(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetReservationByIdRequest(id), cancellationToken);

    /// <summary>Rezervasyon sihirbazı — yeni rezervasyon oluşturur.</summary>
    /// <remarks>
    /// Oda müsaitliği doğrulanır: çakışan tarih veya servis dışı oda <b>409</b>; oda/misafir
    /// aktif otelde değilse <b>404</b>; kapasite aşımı <b>400</b>. Rezervasyon numarası otel
    /// bazında üretilir (<c>RES-{yıl}-{sıra}</c>) ve folio (açık hesap) aynı işlemde açılır.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.ReservationsCreate)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationResponse>> Create(
        [FromBody] CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var created = await dispatcher.Send(request, cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetReservationById), new { id = created.Id }, created);
    }

    /// <summary>Tarih / oda / kişi / kanal değişikliği — müsaitlik ve tutar yeniden hesaplanır.</summary>
    /// <remarks>
    /// Durum bu uçtan değiştirilemez. Nihai durumdaki (<c>CheckedOut</c>, <c>Cancelled</c>,
    /// <c>NoShow</c>) rezervasyon güncellenemez → <b>409</b>.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.ReservationsCreate)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReservationResponse> Update(
        Guid id,
        [FromBody] UpdateReservationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return dispatcher.Send(request with { Id = id }, cancellationToken);
    }

    /// <summary>Check-in: <c>Option</c>/<c>Confirmed</c> → <c>CheckedIn</c>.</summary>
    /// <remarks>
    /// Giriş tarihinden <b>önce</b> check-in denemesi <b>409</b>; oda servis dışıysa <b>409</b>;
    /// başka bir durumdan geçiş <b>409</b>.
    /// </remarks>
    [HttpPost("{id:guid}/check-in")]
    [Authorize(Policy = Permissions.ReservationsCheckInOut)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReservationResponse> CheckIn(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new CheckInReservationRequest(id), cancellationToken);

    /// <summary>Check-out: <c>CheckedIn</c> → <c>CheckedOut</c>; oda <c>Dirty</c>'ye geçer.</summary>
    /// <remarks>
    /// Odanın kat hizmetleri durumu <b>aynı işlemde</b> <c>Dirty</c> olur (architecture.md §5).
    /// Folio kapatılmaz: fatura henüz yok, açık hesap durur.
    /// </remarks>
    [HttpPost("{id:guid}/check-out")]
    [Authorize(Policy = Permissions.ReservationsCheckInOut)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReservationResponse> CheckOut(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new CheckOutReservationRequest(id), cancellationToken);

    /// <summary>İptal: <c>Option</c>/<c>Confirmed</c> → <c>Cancelled</c>.</summary>
    /// <remarks>
    /// <c>CheckedIn</c> / <c>CheckedOut</c> rezervasyon iptal edilemez → <b>409</b>. Kayıt
    /// silinmez, numarası korunur ve oda takviminden düşer.
    /// </remarks>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Permissions.ReservationsCreate)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReservationResponse> Cancel(
        Guid id,
        [FromBody] CancelReservationRequest? request,
        CancellationToken cancellationToken) =>
        // Gövde opsiyoneldir: iptal gerekcesi verilmeden de iptal edilebilir.
        dispatcher.Send((request ?? new CancelReservationRequest()) with { Id = id }, cancellationToken);

    /// <summary>Gelmedi (no-show): <c>Option</c>/<c>Confirmed</c> → <c>NoShow</c>.</summary>
    [HttpPost("{id:guid}/no-show")]
    [Authorize(Policy = Permissions.ReservationsCheckInOut)]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReservationResponse> NoShow(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new MarkNoShowRequest(id), cancellationToken);

    /// <summary>Folio (açık hesap): satırlar + net/KDV/brüt toplamlar.</summary>
    [HttpGet("{id:guid}/folio")]
    [Authorize(Policy = Permissions.ReservationsView)]
    [ProducesResponseType<FolioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<FolioResponse> GetFolio(Guid id, CancellationToken cancellationToken) =>
        dispatcher.Send(new GetReservationFolioRequest(id), cancellationToken);
}
