using FluentValidation;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Create;

/// <summary>
/// İki oluşturma yolu <b>birbirini dışlar</b>: ya <c>reservationId</c> (satırlar folio'dan
/// türetilir) ya da <c>lineItems</c> (elle giriş). İkisi birden ya da hiçbiri → 400. Böylece
/// "bu tutar nereden geldi" sorusunun tek bir cevabı olur (GoBD izlenebilirlik).
/// </summary>
public sealed class CreateInvoiceValidator : InvoiceWriteValidator<CreateInvoiceRequest>
{
    public CreateInvoiceValidator()
    {
        RuleFor(request => request.GuestId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .WithMessage("Rezervasyon verilmediginde misafir (guestId) zorunludur.")
            .When(request => request.ReservationId is null);

        RuleFor(request => request.LineItems)
            .Must(items => items is null || items.Count == 0)
            .WithMessage(
                "Rezervasyondan uretilen faturaya elle satir gonderilemez; " +
                "satirlar rezervasyon ve folio'dan turetilir.")
            .When(request => request.ReservationId is not null);

        RuleFor(request => request.LineItems)
            .Must(items => items is { Count: > 0 })
            .WithMessage(
                "Fatura satirsiz olusturulamaz: ya reservationId ya da en az bir lineItems ogesi verilmelidir.")
            .When(request => request.ReservationId is null);
    }
}
