using FluentValidation;
using HotelCore.Application.Features.Employees.Common;

namespace HotelCore.Application.Features.Employees.Update;

public sealed class UpdateEmployeeValidator : EmployeeWriteValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeValidator() => RuleFor(request => request.Id).NotEmpty();
}
