using FluentValidation;

namespace HotelCore.Application.Features.Reservations.NoShow;

public sealed class MarkNoShowValidator : AbstractValidator<MarkNoShowRequest>
{
    public MarkNoShowValidator() => RuleFor(request => request.Id).NotEmpty();
}
