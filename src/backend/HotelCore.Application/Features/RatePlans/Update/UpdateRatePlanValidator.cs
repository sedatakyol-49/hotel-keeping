using FluentValidation;
using HotelCore.Application.Features.RatePlans.Common;

namespace HotelCore.Application.Features.RatePlans.Update;

public sealed class UpdateRatePlanValidator : RatePlanWriteValidator<UpdateRatePlanRequest>
{
    public UpdateRatePlanValidator() => RuleFor(request => request.Id).NotEmpty();
}
