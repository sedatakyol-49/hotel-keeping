using FluentValidation;

namespace HotelCore.Application.Features.Employees.Common;

/// <summary>
/// Çalışan yazma kuralları tek yerde — api-contracts.md → "Personel".
/// Benzersizlik (<c>staffNumber</c>) ve departmanın aynı otelde olması handler'da kontrol edilir.
/// </summary>
public abstract class EmployeeWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IEmployeeWriteRequest
{
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 200;
    private const int MaxPhoneLength = 50;
    private const int MaxStaffNumberLength = 20;
    private const int MaxAnnualLeaveDays = 60;

    protected EmployeeWriteValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(request => request.Phone).MaximumLength(MaxPhoneLength);
        RuleFor(request => request.StaffNumber).MaximumLength(MaxStaffNumberLength);
        RuleFor(request => request.DepartmentId).NotEmpty();
        RuleFor(request => request.EmploymentType).IsInEnum();
        RuleFor(request => request.AnnualLeaveDays).InclusiveBetween(0m, MaxAnnualLeaveDays);
        RuleFor(request => request.HiredOn).NotEmpty();

        RuleFor(request => request.Email)
            .MaximumLength(MaxEmailLength)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));

        // Ayrilis tarihi ise baslama tarihinden once olamaz.
        RuleFor(request => request.TerminatedOn)
            .GreaterThanOrEqualTo(request => request.HiredOn)
            .When(request => request.TerminatedOn is not null);
    }
}
