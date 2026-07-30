using FluentValidation;

namespace HotelCore.Application.Features.Vacations.GetById;

public sealed class GetVacationByIdValidator : AbstractValidator<GetVacationByIdRequest>
{
    public GetVacationByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
