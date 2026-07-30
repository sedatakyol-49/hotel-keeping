using System.Text.Json.Serialization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.AddPayment;

/// <summary>
/// <c>POST /api/v1/invoices/{id}/payments</c> — faturaya ödeme kaydeder.
/// <para>
/// Kısmi ödeme desteklenir (çoklu kayıt). Toplam ödeme brüt tutara <b>ulaştığında</b> fatura
/// <c>Paid</c> olur; brüt tutarı <b>aşan</b> ödeme <b>409</b> ile reddedilir.
/// </para>
/// </summary>
public sealed record AddInvoicePaymentRequest : IRequest<InvoiceDetailResponse>
{
    /// <summary>Route'tan doldurulur; istek gövdesinden OKUNMAZ.</summary>
    [JsonIgnore]
    public Guid InvoiceId { get; init; }

    /// <summary>Ödeme yöntemi: <c>Cash | Card | Transfer</c>.</summary>
    public PaymentMethod Method { get; init; } = PaymentMethod.Card;

    /// <summary>Ödenen tutar (fatura para biriminde, &gt; 0).</summary>
    public decimal Amount { get; init; }

    /// <summary>Ödeme zamanı; verilmezse sunucu saati (UTC). Gelecek tarih kabul edilmez.</summary>
    public DateTimeOffset? PaidAt { get; init; }

    /// <summary>Terminal/havale referansı (opsiyonel, ≤ 128).</summary>
    public string? Reference { get; init; }
}
