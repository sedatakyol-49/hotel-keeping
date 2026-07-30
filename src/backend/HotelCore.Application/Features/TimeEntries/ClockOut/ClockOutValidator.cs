using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.ClockOut;

/// <summary>
/// Çıkış kaydı kuralları. "Çıkış &gt; giriş" ve "mola çalışma süresini aşamaz" kuralları giriş
/// saatini veritabanından okumayı gerektirdiği için handler'da (<see cref="TimeEntryRules"/>)
/// uygulanır; ikisi de 400 döner.
/// </summary>
public sealed class ClockOutValidator : AbstractValidator<ClockOutRequest>
{
    private const int MaxNoteLength = 500;

    public ClockOutValidator(IDateTimeProvider clock)
    {
        RuleFor(request => request.EmployeeId).NotEmpty();
        RuleFor(request => request.Note).MaximumLength(MaxNoteLength);

        RuleFor(request => request.BreakMinutes)
            .InclusiveBetween(0, TimeEntryRules.MaxBreakMinutes)
            .When(request => request.BreakMinutes is not null);

        RuleFor(request => request.ClockOut)
            .Must(clockOut => TimeEntryRules.IsNotInFuture(clockOut, clock))
            .WithMessage(_ => Messages.ClockOutNotInFuture);
    }
}
