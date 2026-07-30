using FluentValidation;
using HotelCore.Application.Common.Models;

namespace HotelCore.Application.Features.Guests.List;

/// <summary>
/// Sayfa numarası/boyutu <see cref="PageQuery"/> tarafından sınırlanır; burada yalnızca
/// açıkça hatalı değerler reddedilir ki istemci sessizce farklı bir sayfa almasın.
/// </summary>
public sealed class ListGuestsValidator : AbstractValidator<ListGuestsRequest>
{
    private const int MaxSearchLength = 100;

    public ListGuestsValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
        RuleFor(request => request.Search).MaximumLength(MaxSearchLength);
    }
}
