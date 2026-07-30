using FluentValidation;

namespace HotelCore.Application.Features.Shifts.Delete;

public sealed class DeleteShiftValidator : AbstractValidator<DeleteShiftRequest>
{
    public DeleteShiftValidator() => RuleFor(request => request.Id).NotEmpty();
}
