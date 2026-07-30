using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Models;

namespace HotelCore.Application.Features.Reservations.List;

/// <summary>
/// Sayfa sınırları <see cref="PageQuery"/> tarafından uygulanır; burada açıkça hatalı değerler
/// reddedilir. <c>to</c> verilmişse <c>from</c>'dan sonra olmalıdır (aksi hâlde sonuç kümesi
/// sessizce boş kalırdı).
/// </summary>
public sealed class ListReservationsValidator : AbstractValidator<ListReservationsRequest>
{
    private const int MaxSearchLength = 100;

    public ListReservationsValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
        RuleFor(request => request.Search).MaximumLength(MaxSearchLength);
        RuleFor(request => request.Status).IsInEnum().When(request => request.Status is not null);
        RuleFor(request => request.Channel).IsInEnum().When(request => request.Channel is not null);

        RuleFor(request => request.To)
            .GreaterThan(request => request.From)
            .WithMessage(_ => Messages.ToAfterFrom)
            .When(request => request.From is not null && request.To is not null);
    }
}
