using FluentValidation;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.Update;

public sealed class UpdateTimeEntryValidator : AbstractValidator<UpdateTimeEntryRequest>
{
    private const int MaxNoteLength = 500;

    public UpdateTimeEntryValidator(IDateTimeProvider clock)
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.ClockIn).NotEmpty();
        RuleFor(request => request.Note).MaximumLength(MaxNoteLength);
        RuleFor(request => request.BreakMinutes)
            .InclusiveBetween(0, TimeEntryRules.MaxBreakMinutes);

        RuleFor(request => request.ClockIn)
            .Must(clockIn => TimeEntryRules.IsNotInFuture(clockIn, clock))
            .WithMessage("Gelecek tarihli mesai girisi kaydedilemez.");

        RuleFor(request => request.ClockOut)
            .Must(clockOut => TimeEntryRules.IsNotInFuture(clockOut, clock))
            .WithMessage("Gelecek tarihli mesai cikisi kaydedilemez.");

        // Cikis > giris kurali burada da kontrol edilir (iki alan da gövdede geldigi icin).
        RuleFor(request => request.ClockOut)
            .GreaterThan(request => request.ClockIn)
            .When(request => request.ClockOut is not null)
            .WithMessage("Cikis saati giris saatinden sonra olmalidir.");
    }
}
