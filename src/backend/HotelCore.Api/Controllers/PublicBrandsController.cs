using HotelCore.Api.Startup;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Public.Common;
using HotelCore.Application.Features.Public.ListBrandHotels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Marka sitesinin public uçları (api-contracts-public-booking.md §2.1).
/// <para>
/// <b>Anonimdir ve <c>Authorization</c> header'ı tamamen yok sayılır:</b> public bir yolda
/// kimlik, tenant kapsamına <b>hiç</b> katılmaz (bkz. <c>PublicTenantScope</c>). Admin token'ı
/// göndermek hiçbir ek veri açmaz.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/public/brands")]
[Produces("application/json")]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = PublicApiDocument.GroupName)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class PublicBrandsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Bir markanın public kanalı açık otelleri (düz dizi, sayfalama yok).</summary>
    /// <remarks>
    /// Marka slug'ı yoksa <b>veya</b> markanın public kanalı açık hiçbir oteli yoksa
    /// <b>404 <c>BRAND_NOT_FOUND</c></b> — iki durum ayırt edilmez, varlık sızdırılmaz.
    /// </remarks>
    [HttpGet("{brandSlug}/hotels")]
    [PublicCache(Cacheable = true)]
    [ProducesResponseType<IReadOnlyList<PublicHotelListItemResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<PublicHotelListItemResponse>> ListHotels(
        string brandSlug,
        CancellationToken cancellationToken) =>
        dispatcher.Send(new PublicListBrandHotelsRequest(brandSlug), cancellationToken);
}
