using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Reports.Common;

namespace HotelCore.Application.Features.Reports.GetRevenueReport;

internal sealed class GetRevenueReportHandler(ReportReader reader)
    : IRequestHandler<GetRevenueReportRequest, RevenueReportResponse>
{
    public Task<RevenueReportResponse> Handle(
        GetRevenueReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetRevenueAsync(request.From, request.To, cancellationToken);
    }
}
