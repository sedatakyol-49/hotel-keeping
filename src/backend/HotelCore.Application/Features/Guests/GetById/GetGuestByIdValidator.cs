using FluentValidation;

namespace HotelCore.Application.Features.Guests.GetById;

public sealed class GetGuestByIdValidator : AbstractValidator<GetGuestByIdRequest>
{
    public GetGuestByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
