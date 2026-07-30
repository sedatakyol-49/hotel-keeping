using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.Availability.Common;

namespace HotelCore.Application.Features.Availability.GetOccupancy;

/// <summary>
/// Grid aralığı sınırlıdır: <c>to &gt; from</c> ve en fazla
/// <see cref="AvailabilityLimits.MaxOccupancyRangeDays"/> gün. Sınır aşılınca <b>400</b> döner —
/// istemci yanlışlıkla yıllık bir matris istediğinde sessizce kırpılmış veri almaz.
/// </summary>
public sealed class GetOccupancyValidator : AbstractValidator<GetOccupancyRequest>
{
    public GetOccupancyValidator()
    {
        RuleFor(request => request.From).NotEmpty();
        RuleFor(request => request.To).NotEmpty();

        RuleFor(request => request.To)
            .GreaterThan(request => request.From)
            .WithMessage(_ => Messages.ToAfterFromDay);

        RuleFor(request => request.To)
            .Must((request, to) =>
                to.DayNumber - request.From.DayNumber <= AvailabilityLimits.MaxOccupancyRangeDays)
            .WithMessage(_ =>
                Messages.OccupancyRangeTooLong(AvailabilityLimits.MaxOccupancyRangeDays))
            .When(request => request.To > request.From);
    }
}
