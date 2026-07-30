using FluentValidation;
using HotelCore.Application.Common.Models;

namespace HotelCore.Application.Features.Employees.List;

/// <summary>
/// Sayfa numarası/boyutu <see cref="PageQuery"/> tarafından sınırlanır; burada yalnızca
/// açıkça hatalı değerler reddedilir ki istemci sessizce farklı bir sayfa almasın.
/// </summary>
public sealed class ListEmployeesValidator : AbstractValidator<ListEmployeesRequest>
{
    private const int MaxSearchLength = 100;

    public ListEmployeesValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
        RuleFor(request => request.Search).MaximumLength(MaxSearchLength);
        RuleFor(request => request.EmploymentType).IsInEnum().When(r => r.EmploymentType is not null);
    }
}
