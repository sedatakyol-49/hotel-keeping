using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Availability.Common;
using HotelCore.Application.Features.Availability.GetAvailability;
using HotelCore.Application.Features.Availability.GetOccupancy;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Müsaitlik ve doluluk uç noktaları (api-contracts-reservations.md → Availability &amp; Occupancy).
/// <para>
/// İki uç aynı controller'da tutulur çünkü ikisi de aynı çakışma mantığını
/// (<c>IAvailabilityService</c>, yarı açık aralık) kullanır; route'lar sözleşmedeki gibi
/// <c>/api/v1/availability</c> ve <c>/api/v1/occupancy</c> olarak ayrı kalır.
/// </para>
/// <para>
/// Her iki uç da <b>aktif otel gerektirir</b>: Head Office kullanıcısı <c>X-Hotel-Id</c>
/// göndermezse hangi otelin takvimi istendiği belirsizdir → <b>400</b>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AvailabilityController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Aralık boyunca müsait odalar + oda tipi bazında sayılar.</summary>
    /// <remarks>
    /// Aralık <b>yarı açıktır</b> <c>[from, to)</c>: <c>to</c> çıkış günüdür, o gece aranmaz.
    /// Bir oda ancak aralıktaki <b>tüm</b> geceler boşsa müsait sayılır; servis dışı odalar
    /// (<c>isOutOfOrder</c>) ve iptal/no-show olmayan rezervasyonlarla kesişen odalar listelenmez.
    /// Yanıtta <b>fiyat alanı yoktur</b> — tutar rezervasyon oluşturulurken sunucuda hesaplanır.
    /// </remarks>
    [HttpGet("availability")]
    [Authorize(Policy = Permissions.ReservationsView)]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<AvailabilityResponse> GetAvailability(
        [FromQuery] GetAvailabilityRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Doluluk grid'i: oda × gün matrisi (rezervasyon takvimi).</summary>
    /// <remarks>
    /// Hücreler <b>seyrek</b> döner (yalnızca dolu geceler); kolon ekseni yanıttaki <c>days</c>
    /// dizisidir. Her hücrede grid render'ı için gereken alanlar bulunur: <c>reservationId</c>,
    /// <c>guestName</c>, <c>status</c>, <c>isArrival</c>, <c>isDeparture</c>.
    /// Aralık en fazla 92 gün olabilir; aşılırsa <b>400</b>.
    /// </remarks>
    [HttpGet("occupancy")]
    [Authorize(Policy = Permissions.ReservationsView)]
    [ProducesResponseType<OccupancyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<OccupancyResponse> GetOccupancy(
        [FromQuery] GetOccupancyRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);
}
