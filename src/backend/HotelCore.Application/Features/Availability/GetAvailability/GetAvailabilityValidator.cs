using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.Availability.Common;

namespace HotelCore.Application.Features.Availability.GetAvailability;

/// <summary>
/// <c>from</c> ve <c>to</c> zorunludur, <c>to &gt; from</c> olmalıdır (en az bir gece) ve aralık
/// <see cref="AvailabilityLimits.MaxAvailabilityRangeDays"/> günü aşamaz.
/// </summary>
public sealed class GetAvailabilityValidator : AbstractValidator<GetAvailabilityRequest>
{
    public GetAvailabilityValidator()
    {
        RuleFor(request => request.From).NotEmpty();
        RuleFor(request => request.To).NotEmpty();

        RuleFor(request => request.To)
            .GreaterThan(request => request.From)
            .WithMessage(_ => Messages.ToAfterFromNight);

        RuleFor(request => request.To)
            .Must((request, to) =>
                to.DayNumber - request.From.DayNumber <= AvailabilityLimits.MaxAvailabilityRangeDays)
            .WithMessage(_ =>
                Messages.AvailabilityRangeTooLong(AvailabilityLimits.MaxAvailabilityRangeDays))
            .When(request => request.To > request.From);
    }
}
