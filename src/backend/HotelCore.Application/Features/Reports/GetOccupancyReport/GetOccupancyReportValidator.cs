using FluentValidation;
using HotelCore.Application.Features.Reports.Common;

namespace HotelCore.Application.Features.Reports.GetOccupancyReport;

/// <summary>Aralık kuralları iki rapor ucunda ortaktır — bkz. <c>ReportRangeRules</c>.</summary>
public sealed class GetOccupancyReportValidator : AbstractValidator<GetOccupancyReportRequest>
{
    public GetOccupancyReportValidator() => ReportRangeRules.Apply(this);
}
