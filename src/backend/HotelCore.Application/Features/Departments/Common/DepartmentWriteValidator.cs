using FluentValidation;

namespace HotelCore.Application.Features.Departments.Common;

/// <summary>
/// Departman yazma kuralları tek yerde — api-contracts.md → "Personel". Create ve Update
/// aynı sınırlara tabidir; benzersizlik kontrolü handler'da yapılır (409).
/// </summary>
public abstract class DepartmentWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IDepartmentWriteRequest
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 500;

    protected DepartmentWriteValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(MaxNameLength);
        RuleFor(request => request.Description).MaximumLength(MaxDescriptionLength);
    }
}
