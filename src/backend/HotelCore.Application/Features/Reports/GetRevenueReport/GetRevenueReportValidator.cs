using FluentValidation;
using HotelCore.Application.Features.Reports.Common;

namespace HotelCore.Application.Features.Reports.GetRevenueReport;

/// <summary>Aralık kuralları iki rapor ucunda ortaktır — bkz. <c>ReportRangeRules</c>.</summary>
public sealed class GetRevenueReportValidator : AbstractValidator<GetRevenueReportRequest>
{
    public GetRevenueReportValidator() => ReportRangeRules.Apply(this);
}
