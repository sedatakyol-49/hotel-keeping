using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reports.Common;

namespace HotelCore.Application.Features.Reports.GetRevenueReport;

/// <summary>
/// <c>GET /api/v1/reports/revenue?from=&amp;to=</c> — ciro, ADR/RevPAR, kanal dağılımı
/// (toplam + gün bazında seri + otel kırılımı).
/// <para>
/// Aralık <b>kapalıdır</b>: <c>from</c> ve <c>to</c> dâhil, en fazla
/// <see cref="ReportDefinitions.MaxRangeDays"/> gün.
/// </para>
/// <para>
/// <b>Ciro kesinleşmiş faturalardan</b> okunur (muhasebe gerçeği); henüz faturalanmamış
/// konaklamalar ciroya girmez ve ayrı bir alanda (<c>unbilledRoomRevenueGross</c>) gösterilir.
/// Kurtaxe gelir değildir, ayrı alandadır. Ayrıntı: <see cref="RevenueRecognition"/>.
/// </para>
/// </summary>
public sealed record GetRevenueReportRequest : IRequest<RevenueReportResponse>, IReportRangeRequest
{
    /// <summary>Aralığın ilk günü (dâhil).</summary>
    public DateOnly From { get; init; }

    /// <summary>Aralığın son günü (<b>dâhil</b>). <c>to == from</c> tek günlük rapordur.</summary>
    public DateOnly To { get; init; }
}
