using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Finalize;

/// <summary>
/// <c>POST /api/v1/invoices/{id}/finalize</c> — taslağı kesinleştirir: boşluksuz numara atanır,
/// <c>IssuedAt</c> damgalanır, durum <c>Finalized</c> olur ve fatura <b>değiştirilemez</b> hâle
/// gelir (GoBD §6.1/§6.2).
/// </summary>
public sealed record FinalizeInvoiceRequest(Guid Id) : IRequest<InvoiceDetailResponse>;
