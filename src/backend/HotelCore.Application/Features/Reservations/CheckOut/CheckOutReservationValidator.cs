using FluentValidation;

namespace HotelCore.Application.Features.Reservations.CheckOut;

public sealed class CheckOutReservationValidator : AbstractValidator<CheckOutReservationRequest>
{
    public CheckOutReservationValidator() => RuleFor(request => request.Id).NotEmpty();
}
