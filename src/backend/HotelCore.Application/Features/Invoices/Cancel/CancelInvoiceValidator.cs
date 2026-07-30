using FluentValidation;

namespace HotelCore.Application.Features.Invoices.Cancel;

public sealed class CancelInvoiceValidator : AbstractValidator<CancelInvoiceRequest>
{
    private const int MaxReasonLength = 500;

    public CancelInvoiceValidator()
    {
        RuleFor(request => request.Id).NotEmpty();
        RuleFor(request => request.Reason).MaximumLength(MaxReasonLength);
    }
}
