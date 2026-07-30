using FluentValidation;

namespace HotelCore.Application.Features.Employees.Delete;

public sealed class DeleteEmployeeValidator : AbstractValidator<DeleteEmployeeRequest>
{
    public DeleteEmployeeValidator() => RuleFor(request => request.Id).NotEmpty();
}
