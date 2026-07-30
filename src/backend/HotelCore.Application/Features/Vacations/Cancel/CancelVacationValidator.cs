using FluentValidation;

namespace HotelCore.Application.Features.Vacations.Cancel;

public sealed class CancelVacationValidator : AbstractValidator<CancelVacationRequest>
{
    private const int MaxDecisionNoteLength = 500;

    public CancelVacationValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.DecisionNote).MaximumLength(MaxDecisionNoteLength);
    }
}
