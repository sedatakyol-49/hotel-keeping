using FluentValidation;

namespace HotelCore.Application.Features.Guests.Delete;

public sealed class DeleteGuestValidator : AbstractValidator<DeleteGuestRequest>
{
    public DeleteGuestValidator() => RuleFor(request => request.Id).NotEmpty();
}
