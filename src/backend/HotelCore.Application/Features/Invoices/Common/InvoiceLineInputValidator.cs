using FluentValidation;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Tek fatura satırının doğrulaması (Create ve Update tarafından paylaşılır).
/// <para>
/// <b>Negatif tutar yok:</b> eksi miktar/fiyat yalnızca iptal faturasında (Stornorechnung)
/// anlamlıdır ve orada <b>sunucu</b> üretir; istemciden asla kabul edilmez.
/// </para>
/// </summary>
public sealed class InvoiceLineInputValidator : AbstractValidator<InvoiceLineInput>
{
    private const int MaxDescriptionLength = 500;

    /// <summary>Kolon <c>decimal(9,2)</c>.</summary>
    private const decimal MaxQuantity = 9_999m;

    /// <summary>Kolon <c>decimal(18,2)</c>; pratikte tek satır için makul üst sınır.</summary>
    private const decimal MaxUnitPrice = 1_000_000m;

    public InvoiceLineInputValidator()
    {
        RuleFor(line => line.Type).IsInEnum();

        RuleFor(line => line.Description)
            .NotEmpty()
            .MaximumLength(MaxDescriptionLength);

        RuleFor(line => line.Quantity)
            .GreaterThan(0m)
            .LessThanOrEqualTo(MaxQuantity);

        RuleFor(line => line.UnitPrice)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(MaxUnitPrice);
    }
}
