using FluentValidation;
using HotelCore.Application.Features.Shifts.Common;

namespace HotelCore.Application.Features.Shifts.Update;

public sealed class UpdateShiftValidator : ShiftWriteValidator<UpdateShiftRequest>
{
    public UpdateShiftValidator() => RuleFor(request => request.Id).NotEmpty();
}
