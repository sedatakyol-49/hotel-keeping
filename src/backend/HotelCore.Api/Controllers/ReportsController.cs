using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reports.Common;
using HotelCore.Application.Features.Reports.GetOccupancyReport;
using HotelCore.Application.Features.Reports.GetRevenueReport;
using HotelCore.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelCore.Api.Controllers;

/// <summary>
/// Raporlama uç noktaları (docs/api-contracts-reports.md).
/// <para>
/// Her iki uç da <b><c>Reports.View</c></b> izni ister; rol adı controller'a hardcode edilmez
/// (architecture.md §7 — policy adı = izin anahtarı). <c>Reports.View</c> yalnızca
/// Admin/HeadOfficeManager/HotelManager/Accountant rollerine verilir; resepsiyon ve
/// housekeeping ciro görmez.
/// </para>
/// <para>
/// <b>Aktif otel zorunlu DEĞİLDİR.</b> Head Office kullanıcısı <c>X-Hotel-Id</c> göndermezse
/// rapor <b>konsolide</b> hesaplanır (erişilebilir tüm oteller) ve yanıttaki <c>scope</c> alanı
/// bunu açıkça söyler; ayrıca her rapor <c>byHotel</c> kırılımı döndürür. Bu, doluluk
/// <i>grid</i>'inden (<c>/api/v1/occupancy</c>) bilinçli bir farktır: matris tek bir otele
/// aitken rapor bir portföy büyüklüğüdür.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Produces("application/json")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ReportsController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Doluluk raporu: satılan / müsait oda-gece, doluluk oranı, günlük seri.</summary>
    /// <remarks>
    /// Aralık <b>kapalıdır</b> (<c>from</c> ve <c>to</c> dâhil, <c>to == from</c> tek gün) ve en
    /// fazla 366 gündür; aşılırsa <b>400</b>. Sayılan geceler bu günlerde <b>başlayan</b>
    /// gecelerdir — çıkış günü gece saymaz (rezervasyon modülünün yarı açık aralık kararı).
    /// <c>Cancelled</c>/<c>NoShow</c> rezervasyonlar satılmış sayılmaz.
    /// Servis dışı odalar müsait kapasiteden <b>düşülür</b>; üç kapasite sayısı
    /// (<c>physicalRoomNights</c>, <c>outOfOrderRoomNights</c>, <c>availableRoomNights</c>)
    /// ayrı ayrı döner ki tüketici kendi tanımını kurabilsin.
    /// </remarks>
    [HttpGet("occupancy")]
    [Authorize(Policy = Permissions.ReportsView)]
    [ProducesResponseType<OccupancyReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<OccupancyReportResponse> GetOccupancyReport(
        [FromQuery] GetOccupancyReportRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);

    /// <summary>Ciro raporu: oda/ekstra geliri, ADR, RevPAR, kanal dağılımı, günlük seri.</summary>
    /// <remarks>
    /// Ciro <b>kesinleşmiş faturalardan</b> hesaplanır (muhasebe gerçeği): <c>Draft</c> sayılmaz,
    /// kesinleştikten sonra iptal edilen fatura ile Stornorechnung'u birlikte sayılıp
    /// <b>netleştirilir</b>. <b>Henüz faturalanmamış konaklamalar bu ciroya girmez</b> —
    /// operasyonel fark <c>unbilledRoomRevenueGross</c> alanında ayrıca gösterilir.
    /// Kurtaxe gelir değildir (<c>cityTaxCollected</c> ayrı alandır, ADR'ye girmez).
    /// </remarks>
    [HttpGet("revenue")]
    [Authorize(Policy = Permissions.ReportsView)]
    [ProducesResponseType<RevenueReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<RevenueReportResponse> GetRevenueReport(
        [FromQuery] GetRevenueReportRequest request,
        CancellationToken cancellationToken) => dispatcher.Send(request, cancellationToken);
}
