using FluentValidation;

namespace HotelCore.Application.Features.Reservations.GetById;

public sealed class GetReservationByIdValidator : AbstractValidator<GetReservationByIdRequest>
{
    public GetReservationByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
