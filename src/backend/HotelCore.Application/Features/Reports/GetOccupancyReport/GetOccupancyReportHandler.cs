using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reports.Common;

namespace HotelCore.Application.Features.Reports.GetOccupancyReport;

internal sealed class GetOccupancyReportHandler(ReportReader reader)
    : IRequestHandler<GetOccupancyReportRequest, OccupancyReportResponse>
{
    public Task<OccupancyReportResponse> Handle(
        GetOccupancyReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetOccupancyAsync(request.From, request.To, cancellationToken);
    }
}
