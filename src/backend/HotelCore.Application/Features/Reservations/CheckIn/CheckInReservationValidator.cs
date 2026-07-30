using FluentValidation;

namespace HotelCore.Application.Features.Reservations.CheckIn;

public sealed class CheckInReservationValidator : AbstractValidator<CheckInReservationRequest>
{
    public CheckInReservationValidator() => RuleFor(request => request.Id).NotEmpty();
}
