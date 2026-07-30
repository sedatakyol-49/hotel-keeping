using FluentValidation;

namespace HotelCore.Application.Features.Rooms.UpdateHousekeeping;

/// <summary>
/// <c>status</c> tanımlı bir enum değeri olmalıdır (sözleşme: <c>Clean | Dirty | Inspected |
/// OutOfOrder</c>); tanınmayan metin gönderildiğinde model binding zaten 400 üretir.
/// </summary>
public sealed class UpdateHousekeepingValidator : AbstractValidator<UpdateHousekeepingRequest>
{
    private const int MaxNoteLength = 500;

    public UpdateHousekeepingValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Status).IsInEnum();
        RuleFor(request => request.Note).MaximumLength(MaxNoteLength);
    }
}
