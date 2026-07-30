using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;

namespace HotelCore.Application.Features.Invoices.Create;

/// <summary>
/// <c>POST /api/v1/invoices</c> — <b>Draft</b> fatura oluşturur (numara atanmaz).
/// <para>
/// İki yol birbirini dışlar: <c>reservationId</c> verilirse satırlar folio'dan üretilir,
/// verilmezse <c>lineItems</c> elle girilir (ve <c>guestId</c> zorunlu olur).
/// </para>
/// </summary>
public sealed record CreateInvoiceRequest : IRequest<InvoiceDetailResponse>, IInvoiceWriteRequest
{
    /// <summary>Rezervasyon: oda ücreti + folio ekstraları + Kurtaxe otomatik üretilir.</summary>
    public Guid? ReservationId { get; init; }

    /// <summary>Misafir. Rezervasyon yolunda rezervasyondan okunur.</summary>
    public Guid? GuestId { get; init; }

    /// <summary>Fatura dili (<c>de|en|tr</c>); verilmezse misafir → otel varsayılanı.</summary>
    public string? Culture { get; init; }

    /// <summary>Elle girilen satırlar (rezervasyon yolunda boş olmalıdır).</summary>
    public IReadOnlyList<InvoiceLineInput> LineItems { get; init; } = [];
}
