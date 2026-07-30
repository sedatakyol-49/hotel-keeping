using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.List;

internal sealed class ListInvoicesHandler(InvoiceReader reader)
    : IRequestHandler<ListInvoicesRequest, PagedResult<InvoiceResponse>>
{
    public Task<PagedResult<InvoiceResponse>> Handle(
        ListInvoicesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return reader.ListAsync(request.ToQuery(), cancellationToken);
    }
}
