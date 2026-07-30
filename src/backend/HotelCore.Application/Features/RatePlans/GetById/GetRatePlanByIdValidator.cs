using FluentValidation;

namespace HotelCore.Application.Features.RatePlans.GetById;

public sealed class GetRatePlanByIdValidator : AbstractValidator<GetRatePlanByIdRequest>
{
    public GetRatePlanByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
