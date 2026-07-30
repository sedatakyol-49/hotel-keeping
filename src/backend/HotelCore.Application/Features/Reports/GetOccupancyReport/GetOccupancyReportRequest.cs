using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reports.Common;

namespace HotelCore.Application.Features.Reports.GetOccupancyReport;

/// <summary>
/// <c>GET /api/v1/reports/occupancy?from=&amp;to=</c> — oda-gece, kapasite ve doluluk oranı
/// (toplam + gün bazında seri + otel kırılımı).
/// <para>
/// Aralık <b>kapalıdır</b>: <c>from</c> ve <c>to</c> dâhil, en fazla
/// <see cref="ReportDefinitions.MaxRangeDays"/> gün. Sayılan geceler bu günlerde <b>başlayan</b>
/// gecelerdir (çıkış günü gece saymaz).
/// </para>
/// <para>
/// Doluluk <i>grid</i>'i (<c>GET /api/v1/occupancy</c>) ile karıştırılmamalıdır: o bir
/// oda × gün <b>matrisi</b>dir, aktif otel gerektirir ve 92 günle sınırlıdır. Bu uç ise
/// toplulaştırılmış bir <b>rapordur</b> ve konsolide modda da çalışır.
/// </para>
/// </summary>
public sealed record GetOccupancyReportRequest : IRequest<OccupancyReportResponse>, IReportRangeRequest
{
    /// <summary>Aralığın ilk günü (dâhil).</summary>
    public DateOnly From { get; init; }

    /// <summary>Aralığın son günü (<b>dâhil</b>). <c>to == from</c> tek günlük rapordur.</summary>
    public DateOnly To { get; init; }
}
