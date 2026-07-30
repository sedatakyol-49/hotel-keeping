using FluentValidation;
using HotelCore.Application.Features.Departments.Common;

namespace HotelCore.Application.Features.Departments.Update;

public sealed class UpdateDepartmentValidator : DepartmentWriteValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentValidator() => RuleFor(request => request.Id).NotEmpty();
}
