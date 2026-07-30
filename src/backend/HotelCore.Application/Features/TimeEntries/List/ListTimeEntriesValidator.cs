using FluentValidation;
using HotelCore.Application.Common.Models;

namespace HotelCore.Application.Features.TimeEntries.List;

public sealed class ListTimeEntriesValidator : AbstractValidator<ListTimeEntriesRequest>
{
    public ListTimeEntriesValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);

        // Ters aralik sessizce bos liste dondurmek yerine hata verir.
        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From)
            .When(request => request.From is not null && request.To is not null);
    }
}
