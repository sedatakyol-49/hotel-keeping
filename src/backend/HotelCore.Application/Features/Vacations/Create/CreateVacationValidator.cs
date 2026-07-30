using FluentValidation;
using HotelCore.Application.Features.Vacations.Common;

namespace HotelCore.Application.Features.Vacations.Create;

/// <summary>
/// İzin talebi yazma kuralları. Çakışma kontrolü (409) ve çalışanın aynı otelde olması (404)
/// veritabanı gerektirdiği için handler'da yapılır.
/// </summary>
public sealed class CreateVacationValidator : AbstractValidator<CreateVacationRequest>
{
    private const int MaxReasonLength = 500;

    public CreateVacationValidator()
    {
        RuleFor(request => request.EmployeeId).NotEmpty();
        RuleFor(request => request.From).NotEmpty();
        RuleFor(request => request.To).NotEmpty();
        RuleFor(request => request.Reason).MaximumLength(MaxReasonLength);

        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From)
            .WithMessage("Izin bitisi baslangictan once olamaz.");

        // RequestedDays precision(5,2) ile sinirli; ayrica yillik izin bir yili asmaz.
        RuleFor(request => request.To)
            .Must((request, to) =>
                VacationDays.Calculate(request.From, to) <= VacationDays.MaxDaysPerRequest)
            .When(request => request.To >= request.From)
            .WithMessage($"Bir izin talebi en fazla {VacationDays.MaxDaysPerRequest} gun olabilir.");
    }
}
