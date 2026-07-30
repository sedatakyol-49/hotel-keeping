using FluentValidation;

namespace HotelCore.Application.Features.Invoices.List;

/// <summary>
/// Liste parametreleri. <c>page</c>/<c>pageSize</c> hatalıysa <c>PageQuery</c> sessizce sınırlara
/// çeker; burada yalnızca anlamsız girdiler reddedilir.
/// </summary>
public sealed class ListInvoicesValidator : AbstractValidator<ListInvoicesRequest>
{
    /// <summary>Fatura numarası (32) veya misafir adı (100) için yeterli üst sınır.</summary>
    private const int MaxSearchLength = 100;

    public ListInvoicesValidator()
    {
        RuleFor(request => request.Search).MaximumLength(MaxSearchLength);

        RuleFor(request => request.To)
            .GreaterThanOrEqualTo(request => request.From!.Value)
            .When(request => request.From is not null && request.To is not null)
            .WithMessage("'to' tarihi 'from' tarihinden once olamaz.");
    }
}
