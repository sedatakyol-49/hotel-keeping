using FluentValidation;

namespace HotelCore.Application.Features.Vacations.Balances;

public sealed class ListVacationBalancesValidator : AbstractValidator<ListVacationBalancesRequest>
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    public ListVacationBalancesValidator() =>
        RuleFor(request => request.Year)
            .InclusiveBetween(MinYear, MaxYear)
            .When(request => request.Year is not null);
}
