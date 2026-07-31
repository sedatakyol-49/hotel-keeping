using HotelCore.Api.Services;
using HotelCore.Api.Startup;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.CancelBooking;
using HotelCore.Application.Features.Public.Common;
using HotelCore.Application.Features.Public.CreateBooking;
using HotelCore.Application.Features.Public.CreateHold;
using HotelCore.Application.Features.Public.GetAvailability;
using HotelCore.Application.Features.Public.GetBooking;
using HotelCore.Application.Features.Public.GetHold;
using HotelCore.Application.Features.Public.GetHotel;
using HotelCore.Application.Features.Public.GetLegal;
using HotelCore.Application.Features.Public.GetRoomType;
using HotelCore.Application.Features.Public.ListRoomTypes;
using HotelCore.Application.Features.Public.LookupBooking;
using HotelCore.Application.Features.Public.ReleaseHold;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Misafire açık rezervasyon kanalının otel kapsamlı uçları
/// (api-contracts-public-booking.md §2–§7).
///
/// <para><b>Aktif otel yol parametresindedir.</b> <c>hotelSlug</c> hiçbir action'a argüman
/// olarak geçmez: <c>PublicTenantMiddleware</c> onu tenant kapsamına çevirir ve handler'lar
/// <c>ITenantContext</c>'ten okur. Böylece "yolda A oteli, gövdede B oteli" tutarsızlığı
/// yapısal olarak imkânsızdır.</para>
///
/// <para><b>Bu uçlar 401 ve 403 ÜRETMEZ.</b> 403, sorulan kaynağın var olduğunu doğrular; public
/// tarafta her yetki/varlık sorunu <b>404</b>'e indirgenir.</para>
///
/// <para><b>Hiçbir çerez konmaz</b> (§25 TDDDG): uçlar tamamen durumsuzdur, hold ve booking
/// token'ları yanıt <b>gövdesinde</b> döner.</para>
/// </summary>
[ApiController]
[Route("api/v1/public/hotels/{hotelSlug}")]
[Produces("application/json")]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = PublicApiDocument.GroupName)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class PublicHotelsController(IDispatcher dispatcher, PublicClientAddress clientAddress)
    : ControllerBase
{
    // -------------------------------------------------------------------------------------------
    // Künye, hukuki bilgiler ve katalog — CDN'e verilebilir
    // -------------------------------------------------------------------------------------------

    /// <summary>Otel künyesi, rezervasyon sınırları, Kurtaxe ve iptal politikası.</summary>
    /// <remarks>
    /// <b>404 <c>HOTEL_NOT_FOUND</c>:</b> slug yok, otel silinmiş <b>veya</b> public kanal kapalı —
    /// üç durum <b>ayırt edilmez</b>.
    /// </remarks>
    [HttpGet]
    [PublicCache(Cacheable = true)]
    [ProducesResponseType<PublicHotelResponse>(StatusCodes.Status200OK)]
    public Task<PublicHotelResponse> GetHotel(CancellationToken cancellationToken) =>
        dispatcher.Send(new PublicGetHotelRequest(), cancellationToken);

    /// <summary>Impressum (§5 DDG), aydınlatma metni (DSGVO Art. 13) ve AGB.</summary>
    /// <remarks>
    /// Tüm alanlar veritabanından gelir; <b>hiçbiri koda gömülü değildir</b>. Prerender edilen
    /// hukuki sayfaların kaynağıdır.
    /// </remarks>
    [HttpGet("legal")]
    [PublicCache(Cacheable = true)]
    [ProducesResponseType<PublicLegalResponse>(StatusCodes.Status200OK)]
    public Task<PublicLegalResponse> GetLegal(CancellationToken cancellationToken) =>
        dispatcher.Send(new PublicGetLegalRequest(), cancellationToken);

    /// <summary>Oda tipi kataloğu ("ab" fiyatıyla; oda sayısı ve doluluk yoktur).</summary>
    [HttpGet("room-types")]
    [PublicCache(Cacheable = true)]
    [ProducesResponseType<IReadOnlyList<PublicRoomTypeSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PublicRoomTypeSummaryResponse>> ListRoomTypes(
        CancellationToken cancellationToken) =>
        dispatcher.Send(new PublicListRoomTypesRequest(), cancellationToken);

    /// <summary>Oda tipi detayı; kod büyük/küçük harf duyarsızdır.</summary>
    [HttpGet("room-types/{roomTypeCode}")]
    [PublicCache(Cacheable = true)]
    [ProducesResponseType<PublicRoomTypeDetailResponse>(StatusCodes.Status200OK)]
    public Task<PublicRoomTypeDetailResponse> GetRoomType(
        string roomTypeCode,
        CancellationToken cancellationToken) =>
        dispatcher.Send(new PublicGetRoomTypeRequest(roomTypeCode), cancellationToken);

    // -------------------------------------------------------------------------------------------
    // Müsaitlik — tarihe bağlı, ASLA cache'lenmez
    // -------------------------------------------------------------------------------------------

    /// <summary>Arama + fiyat teklifi. <b>Hold oluşturmaz.</b></summary>
    /// <remarks>
    /// Hiçbir tip müsait değilse <c>offers: []</c> ile <b>200</b> döner (404 değil).
    /// Müsait oda sayısı <b>5'te kırpılır</b>.
    /// </remarks>
    [HttpGet("availability")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType<PublicAvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PublicAvailabilityResponse> GetAvailability(
        [FromQuery] PublicGetAvailabilityRequest request,
        CancellationToken cancellationToken) =>
        dispatcher.Send(request, cancellationToken);

    // -------------------------------------------------------------------------------------------
    // Geçici tutma (hold)
    // -------------------------------------------------------------------------------------------

    /// <summary>Teklifi 15 dakika dondurur ve somut bir odayı tutar.</summary>
    /// <remarks>
    /// Kişisel veri <b>yazılmaz</b>. Uygun oda yoksa <b>409 <c>ROOM_NO_LONGER_AVAILABLE</c></b>,
    /// kapasite aşımında <b>409 <c>CAPACITY_EXCEEDED</c></b>.
    /// </remarks>
    [HttpPost("holds")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType<PublicHoldResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublicHoldResponse>> CreateHold(
        string hotelSlug,
        [FromBody] PublicCreateHoldRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // İstemci IP'si GÖVDEDEN alınmaz: yalnızca tuzlanmış özeti saklanır.
        var created = await dispatcher
            .Send(request with { ClientIp = clientAddress.Resolve(HttpContext) }, cancellationToken)
            .ConfigureAwait(false);

        return Created(
            $"/api/v1/public/hotels/{hotelSlug}/holds/{created.HoldToken}",
            created);
    }

    /// <summary>Kalan süre ve donmuş teklif (yeni fiyat hesaplanmaz).</summary>
    /// <remarks>
    /// Süresi dolmuşsa <b>409 <c>HOLD_EXPIRED</c></b>, tüketilmişse <b>409
    /// <c>HOLD_ALREADY_USED</c></b>, bulunamazsa <b>404 <c>HOLD_NOT_FOUND</c></b>.
    /// </remarks>
    [HttpGet("holds/{holdToken}")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType<PublicHoldResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<PublicHoldResponse> GetHold(string holdToken, CancellationToken cancellationToken) =>
        dispatcher.Send(new PublicGetHoldRequest(holdToken), cancellationToken);

    /// <summary>Tutmayı bırakır (idempotent: bilinmeyen token da 204).</summary>
    [HttpDelete("holds/{holdToken}")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseHold(string holdToken, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new PublicReleaseHoldRequest(holdToken), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    // -------------------------------------------------------------------------------------------
    // Rezervasyon
    // -------------------------------------------------------------------------------------------

    /// <summary>Rezervasyon oluşturur (tek transaction).</summary>
    /// <remarks>
    /// <b>Kart alanı yoktur ve eklenmeyecektir:</b> gövdede kart alanı adı geçerse istek
    /// <b>400 <c>CARD_DATA_NOT_ACCEPTED</c></b> ile reddedilir ve gövde loglanmaz.
    /// Kişi sayısı ve tarihler istekten değil <b>hold'dan</b> okunur.
    /// </remarks>
    [HttpPost("bookings")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType<PublicBookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublicBookingResponse>> CreateBooking(
        string hotelSlug,
        [FromBody] PublicCreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var created = await dispatcher
            .Send(request with { ClientIp = clientAddress.Resolve(HttpContext) }, cancellationToken)
            .ConfigureAwait(false);

        return Created(
            $"/api/v1/public/hotels/{hotelSlug}/bookings/{created.AccessToken}",
            created);
    }

    /// <summary>Rezervasyon sorgulama (yanıt <c>accessToken</c> alanını taşımaz).</summary>
    [HttpGet("bookings/{accessToken}")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType<PublicBookingResponse>(StatusCodes.Status200OK)]
    public Task<PublicBookingResponse> GetBooking(
        string accessToken,
        CancellationToken cancellationToken) =>
        dispatcher.Send(new PublicGetBookingRequest(accessToken), cancellationToken);

    /// <summary>Online iptal.</summary>
    /// <remarks>
    /// Ücret doğacaksa <c>acknowledgedFeeAmount</c> zorunludur ve sunucunun hesabıyla
    /// eşleşmelidir; aksi hâlde <b>409 <c>FEE_ACKNOWLEDGEMENT_REQUIRED</c></b>. Ücret matrahı
    /// <b>yalnızca konaklama tutarıdır</b>, Kurtaxe girmez.
    /// </remarks>
    [HttpPost("bookings/{accessToken}/cancel")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType<PublicBookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<PublicBookingResponse> CancelBooking(
        string accessToken,
        [FromBody] PublicCancelBookingRequest? request,
        CancellationToken cancellationToken) =>
        // Gövde opsiyoneldir: ücretsiz iptalde gerekçe ve tutar teyidi gerekmez.
        dispatcher.Send(
            (request ?? new PublicCancelBookingRequest()) with { AccessToken = accessToken },
            cancellationToken);

    /// <summary>Erişim bağlantısını e-postayla yeniden gönderir.</summary>
    /// <remarks>
    /// <b>Hiçbir koşulda veri döndürmez:</b> eşleşme olsun olmasın <b>202</b> ve gövdesiz yanıt,
    /// ayrıca <b>sabit minimum işlem süresi</b> (zamanlama da varlık sızdırmaz).
    /// </remarks>
    [HttpPost("bookings/lookup")]
    [PublicCache(Cacheable = false)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> LookupBooking(
        [FromBody] PublicLookupBookingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await dispatcher
            .Send(request with { ClientIp = clientAddress.Resolve(HttpContext) }, cancellationToken)
            .ConfigureAwait(false);

        return Accepted();
    }
}
