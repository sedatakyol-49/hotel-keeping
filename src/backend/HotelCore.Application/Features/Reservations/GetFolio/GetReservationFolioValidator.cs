using FluentValidation;

namespace HotelCore.Application.Features.Reservations.GetFolio;

public sealed class GetReservationFolioValidator : AbstractValidator<GetReservationFolioRequest>
{
    public GetReservationFolioValidator() => RuleFor(request => request.Id).NotEmpty();
}
