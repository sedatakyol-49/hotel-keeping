using FluentValidation;

namespace HotelCore.Application.Features.Invoices.AddPayment;

public sealed class AddInvoicePaymentValidator : AbstractValidator<AddInvoicePaymentRequest>
{
    private const int MaxReferenceLength = 128;

    /// <summary>Tek ödeme için makul üst sınır; kolon <c>decimal(18,2)</c>.</summary>
    private const decimal MaxAmount = 1_000_000m;

    public AddInvoicePaymentValidator()
    {
        RuleFor(request => request.InvoiceId).NotEmpty();
        RuleFor(request => request.Method).IsInEnum();

        // Negatif/sifir odeme kabul edilmez: iade akisi bu fazda yok (bkz. sozlesme notu).
        RuleFor(request => request.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(MaxAmount);

        RuleFor(request => request.Reference).MaximumLength(MaxReferenceLength);
    }
}
