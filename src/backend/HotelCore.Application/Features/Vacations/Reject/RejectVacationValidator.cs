using FluentValidation;

namespace HotelCore.Application.Features.Vacations.Reject;

public sealed class RejectVacationValidator : AbstractValidator<RejectVacationRequest>
{
    private const int MaxDecisionNoteLength = 500;

    public RejectVacationValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.DecisionNote).MaximumLength(MaxDecisionNoteLength);
    }
}
