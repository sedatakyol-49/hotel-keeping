using FluentValidation;

namespace HotelCore.Application.Features.Reservations.Cancel;

public sealed class CancelReservationValidator : AbstractValidator<CancelReservationRequest>
{
    private const int MaxReasonLength = 500;

    public CancelReservationValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Reason).MaximumLength(MaxReasonLength);
    }
}
