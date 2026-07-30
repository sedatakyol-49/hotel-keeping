using FluentValidation;

namespace HotelCore.Application.Features.Invoices.Finalize;

public sealed class FinalizeInvoiceValidator : AbstractValidator<FinalizeInvoiceRequest>
{
    public FinalizeInvoiceValidator() => RuleFor(request => request.Id).NotEmpty();
}
