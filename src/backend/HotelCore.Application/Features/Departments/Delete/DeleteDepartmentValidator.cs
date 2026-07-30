using FluentValidation;

namespace HotelCore.Application.Features.Departments.Delete;

public sealed class DeleteDepartmentValidator : AbstractValidator<DeleteDepartmentRequest>
{
    public DeleteDepartmentValidator() => RuleFor(request => request.Id).NotEmpty();
}
