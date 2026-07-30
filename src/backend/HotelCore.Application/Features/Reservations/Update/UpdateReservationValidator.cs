using FluentValidation;
using HotelCore.Application.Features.Reservations.Common;

namespace HotelCore.Application.Features.Reservations.Update;

public sealed class UpdateReservationValidator : ReservationWriteValidator<UpdateReservationRequest>
{
    public UpdateReservationValidator() => RuleFor(request => request.Id).NotEmpty();
}
