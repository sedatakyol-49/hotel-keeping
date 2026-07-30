using FluentValidation;
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
            .WithMessage("'to' tarihi 'from' tarihinden sonra olmalidir (en az 1 gece).");

        RuleFor(request => request.To)
            .Must((request, to) =>
                to.DayNumber - request.From.DayNumber <= AvailabilityLimits.MaxAvailabilityRangeDays)
            .WithMessage(
                $"Tarih araligi en fazla {AvailabilityLimits.MaxAvailabilityRangeDays} gun olabilir.")
            .When(request => request.To > request.From);
    }
}
