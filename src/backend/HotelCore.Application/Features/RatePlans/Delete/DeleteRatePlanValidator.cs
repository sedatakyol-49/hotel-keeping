using FluentValidation;

namespace HotelCore.Application.Features.RatePlans.Delete;

public sealed class DeleteRatePlanValidator : AbstractValidator<DeleteRatePlanRequest>
{
    public DeleteRatePlanValidator() => RuleFor(request => request.Id).NotEmpty();
}
