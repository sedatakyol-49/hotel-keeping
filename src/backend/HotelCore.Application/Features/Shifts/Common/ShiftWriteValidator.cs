using FluentValidation;

namespace HotelCore.Application.Features.Shifts.Common;

/// <summary>
/// Vardiya yazma kuralları tek yerde. Aynı çalışana aynı gün ikinci vardiya (409) ve çalışanın
/// aktif otelde olması (404) veritabanı gerektirdiği için handler'da kontrol edilir.
/// </summary>
public abstract class ShiftWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IShiftWriteRequest
{
    private const int MaxNoteLength = 500;

    protected ShiftWriteValidator()
    {
        RuleFor(request => request.EmployeeId).NotEmpty();
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.ShiftType).IsInEnum();
        RuleFor(request => request.Note).MaximumLength(MaxNoteLength);
    }
}
