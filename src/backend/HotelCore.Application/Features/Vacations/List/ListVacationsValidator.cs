using FluentValidation;
using HotelCore.Application.Common.Models;

namespace HotelCore.Application.Features.Vacations.List;

/// <summary>
/// Sayfa numarası/boyutu <see cref="PageQuery"/> tarafından sınırlanır; burada yalnızca
/// açıkça hatalı değerler reddedilir ki istemci sessizce farklı bir sayfa almasın.
/// </summary>
public sealed class ListVacationsValidator : AbstractValidator<ListVacationsRequest>
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    public ListVacationsValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
        RuleFor(request => request.Status).IsInEnum().When(request => request.Status is not null);
        RuleFor(request => request.Year)
            .InclusiveBetween(MinYear, MaxYear)
            .When(request => request.Year is not null);

        // Ters aralik sessizce bos liste dondurmek yerine hata verir.
        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From)
            .When(request => request.From is not null && request.To is not null);
    }
}
