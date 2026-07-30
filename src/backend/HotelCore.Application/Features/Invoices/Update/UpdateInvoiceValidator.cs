using FluentValidation;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Update;

public sealed class UpdateInvoiceValidator : InvoiceWriteValidator<UpdateInvoiceRequest>
{
    public UpdateInvoiceValidator()
    {
        RuleFor(request => request.Id).NotEmpty();

        RuleFor(request => request.GuestId)
            .NotEqual(Guid.Empty)
            .When(request => request.GuestId is not null);

        // PUT tam degisim: satirsiz bir fatura anlamsizdir.
        RuleFor(request => request.LineItems)
            .Must(items => items is { Count: > 0 })
            .WithMessage(_ => Messages.InvoiceNeedsLines);
    }
}
