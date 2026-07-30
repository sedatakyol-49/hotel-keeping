using HotelCore.Application.Common.Models;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura listesi filtresi (istek DTO'sundan türetilir; reader yalnızca bunu bilir).
/// </summary>
/// <param name="Paging">Sınırları uygulanmış sayfalama.</param>
/// <param name="Status">Durum filtresi.</param>
/// <param name="GuestId">Misafir filtresi.</param>
/// <param name="ReservationId">Rezervasyon filtresi.</param>
/// <param name="From">
/// <c>IssuedAt</c> alt sınırı (dâhil). Tarih filtresi verildiğinde <b>taslaklar listelenmez</b>:
/// taslakların fatura tarihi henüz yoktur.
/// </param>
/// <param name="To"><c>IssuedAt</c> üst sınırı (gün <b>dâhil</b>; sorguda ertesi güne çevrilir).</param>
/// <param name="Search">Fatura numarası veya misafir adı/soyadında "contains" arama.</param>
internal sealed record InvoiceListQuery(
    PageQuery Paging,
    InvoiceStatus? Status,
    Guid? GuestId,
    Guid? ReservationId,
    DateOnly? From,
    DateOnly? To,
    string? Search);
