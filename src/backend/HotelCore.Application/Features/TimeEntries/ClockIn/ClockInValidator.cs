using FluentValidation;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.ClockIn;

/// <summary>
/// Giriş kaydı kuralları. Zaman <see cref="IDateTimeProvider"/>'dan okunur
/// (<c>DateTimeOffset.UtcNow</c> kullanılmaz) — böylece kural testlerde sabit saatle doğrulanabilir.
/// </summary>
public sealed class ClockInValidator : AbstractValidator<ClockInRequest>
{
    private const int MaxNoteLength = 500;

    public ClockInValidator(IDateTimeProvider clock)
    {
        RuleFor(request => request.EmployeeId).NotEmpty();
        RuleFor(request => request.Note).MaximumLength(MaxNoteLength);

        RuleFor(request => request.ClockIn)
            .Must(clockIn => TimeEntryRules.IsNotInFuture(clockIn, clock))
            .WithMessage("Gelecek tarihli mesai girisi kaydedilemez.");
    }
}
