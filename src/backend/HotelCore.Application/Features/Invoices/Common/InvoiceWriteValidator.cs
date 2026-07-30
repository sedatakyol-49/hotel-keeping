using FluentValidation;
using HotelCore.Application.Common.Localization;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura yazma isteklerinin ortak kuralları (docs/api-contracts-invoices.md → Doğrulama):
/// dil, satır sayısı ve satır içeriği. Yol seçimi (rezervasyon vs. elle) ve kimlik alanları
/// Create/Update validator'larında tanımlanır.
/// </summary>
/// <typeparam name="TRequest">Create veya Update isteği.</typeparam>
public abstract class InvoiceWriteValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : IInvoiceWriteRequest
{
    /// <summary>Tek faturada makul satır sayısı üst sınırı (kötüye kullanım koruması).</summary>
    protected const int MaxLineItems = 200;

    protected InvoiceWriteValidator()
    {
        RuleFor(request => request.Culture)
            .Must(SupportedCultures.IsSupported)
            .WithMessage(_ => Messages.SupportedCultureList)
            .When(request => request.Culture is not null);

        RuleFor(request => request.LineItems)
            .NotNull()
            .Must(items => items is null || items.Count <= MaxLineItems)
            .WithMessage(_ => Messages.InvoiceMaxLineItems(MaxLineItems));

        RuleForEach(request => request.LineItems).SetValidator(new InvoiceLineInputValidator());
    }
}
