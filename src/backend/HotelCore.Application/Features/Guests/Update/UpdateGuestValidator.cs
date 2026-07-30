using FluentValidation;
using HotelCore.Application.Features.Guests.Common;

namespace HotelCore.Application.Features.Guests.Update;

public sealed class UpdateGuestValidator : GuestWriteValidator<UpdateGuestRequest>
{
    public UpdateGuestValidator() => RuleFor(request => request.Id).NotEmpty();
}
