using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.GetById;

/// <summary>
/// <c>GET /api/v1/invoices/{id}</c> — satırlar, ödemeler ve <b>denetim izi</b> dâhil detay.
/// </summary>
public sealed record GetInvoiceByIdRequest(Guid Id) : IRequest<InvoiceDetailResponse>;
