using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.List;

/// <summary>
/// <c>GET /api/v1/invoices</c> — sayfalı + filtreli fatura listesi.
/// <c>?page=1&amp;pageSize=20&amp;status=&amp;guestId=&amp;reservationId=&amp;from=&amp;to=&amp;search=</c>
/// </summary>
public sealed record ListInvoicesRequest : IRequest<PagedResult<InvoiceResponse>>
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PageQuery.DefaultPageSize;

    /// <summary>Durum filtresi: <c>Draft | Finalized | Paid | Cancelled</c>.</summary>
    public InvoiceStatus? Status { get; init; }

    public Guid? GuestId { get; init; }

    public Guid? ReservationId { get; init; }

    /// <summary>Fatura tarihi alt sınırı (dâhil). Verilirse <b>taslaklar listelenmez</b>.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Fatura tarihi üst sınırı (gün dâhil).</summary>
    public DateOnly? To { get; init; }

    /// <summary>Fatura numarası veya misafir ad/soyadında büyük-küçük harf duyarsız arama.</summary>
    public string? Search { get; init; }

    /// <summary><b>internal:</b> OpenAPI şemasına sızmaması için (bkz. ListRoomsRequest).</summary>
    internal InvoiceListQuery ToQuery() =>
        new(
            new PageQuery { Page = Page, PageSize = PageSize },
            Status,
            GuestId,
            ReservationId,
            From,
            To,
            Search);
}
