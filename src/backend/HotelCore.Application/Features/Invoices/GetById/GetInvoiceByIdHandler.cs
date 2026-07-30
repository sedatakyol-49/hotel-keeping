using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.GetById;

internal sealed class GetInvoiceByIdHandler(InvoiceReader reader)
    : IRequestHandler<GetInvoiceByIdRequest, InvoiceDetailResponse>
{
    public Task<InvoiceDetailResponse> Handle(
        GetInvoiceByIdRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.GetDetailAsync(request.Id, cancellationToken);
    }
}
