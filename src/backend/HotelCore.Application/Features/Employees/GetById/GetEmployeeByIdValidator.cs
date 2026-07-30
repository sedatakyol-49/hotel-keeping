using FluentValidation;

namespace HotelCore.Application.Features.Employees.GetById;

public sealed class GetEmployeeByIdValidator : AbstractValidator<GetEmployeeByIdRequest>
{
    public GetEmployeeByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
