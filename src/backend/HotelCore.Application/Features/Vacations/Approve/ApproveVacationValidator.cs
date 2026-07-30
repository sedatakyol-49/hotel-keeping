using FluentValidation;

namespace HotelCore.Application.Features.Vacations.Approve;

public sealed class ApproveVacationValidator : AbstractValidator<ApproveVacationRequest>
{
    private const int MaxDecisionNoteLength = 500;

    public ApproveVacationValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.DecisionNote).MaximumLength(MaxDecisionNoteLength);
    }
}
