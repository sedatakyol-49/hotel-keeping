using FluentValidation;
using HotelCore.Application.Features.Shifts.Common;

namespace HotelCore.Application.Features.Shifts.GetPlan;

/// <summary>
/// Aralık parametrelerinin doğrulaması. Hatalı <c>week</c> biçimi sessizce geçerli haftaya
/// düşmez: istemci yanlış haftayı doğru sanmasın diye <b>400</b> döner.
/// </summary>
public sealed class GetShiftPlanValidator : AbstractValidator<GetShiftPlanRequest>
{
    public GetShiftPlanValidator()
    {
        RuleFor(request => request.Week)
            .Must(week => ShiftWeek.TryParse(week, out _, out _))
            .When(request => !string.IsNullOrWhiteSpace(request.Week))
            .WithMessage("ISO 8601 hafta bicimi olmalidir (ornek: 2026-W31).");

        // from/to birlikte anlamlidir; tekini gondermek yarim aralik demektir.
        RuleFor(request => request.To)
            .NotNull()
            .When(request => string.IsNullOrWhiteSpace(request.Week) && request.From is not null)
            .WithMessage("'from' ile birlikte 'to' da gonderilmelidir.");

        RuleFor(request => request.From)
            .NotNull()
            .When(request => string.IsNullOrWhiteSpace(request.Week) && request.To is not null)
            .WithMessage("'to' ile birlikte 'from' da gonderilmelidir.");

        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From)
            .When(request => request.From is not null && request.To is not null)
            .WithMessage("'to' tarihi 'from' tarihinden once olamaz.");

        RuleFor(request => request.To)
            .Must((request, to) =>
                to is null
                || request.From is null
                || to.Value.DayNumber - request.From.Value.DayNumber < ShiftPlanRange.MaxRangeDays)
            .WithMessage($"Aralik en fazla {ShiftPlanRange.MaxRangeDays} gun olabilir.");
    }
}
