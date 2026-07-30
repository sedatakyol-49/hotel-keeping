using FluentValidation;

namespace HotelCore.Application.Features.Invoices.GetById;

public sealed class GetInvoiceByIdValidator : AbstractValidator<GetInvoiceByIdRequest>
{
    public GetInvoiceByIdValidator() => RuleFor(request => request.Id).NotEmpty();
}
