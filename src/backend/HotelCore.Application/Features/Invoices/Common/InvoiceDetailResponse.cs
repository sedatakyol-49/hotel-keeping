namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura detayı: liste alanları + satırlar + ödemeler + <b>denetim izi</b> (GoBD §6.3).
/// <para>
/// Liste tipinden türetilmez (kalıtım OpenAPI'de <c>allOf</c> üretir ve frontend client
/// üretimini karmaşıklaştırır); alanlar bilinçli olarak tekrarlanır.
/// </para>
/// </summary>
public sealed record InvoiceDetailResponse
{
    public Guid Id { get; init; }

    /// <summary>Taslakta null — numara finalize anında atanır.</summary>
    public string? InvoiceNumber { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? IssuedAt { get; init; }

    public Guid GuestId { get; init; }

    public string GuestName { get; init; } = string.Empty;

    public Guid? ReservationId { get; init; }

    public string? ReservationNumber { get; init; }

    public string Culture { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    public decimal NetAmount { get; init; }

    public decimal VatAmount { get; init; }

    public decimal CityTaxAmount { get; init; }

    public decimal GrossAmount { get; init; }

    public decimal PaidAmount { get; init; }

    public decimal OutstandingAmount { get; init; }

    public Guid? CancelledByInvoiceId { get; init; }

    public Guid? CancelsInvoiceId { get; init; }

    public bool IsCancellationInvoice { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Fatura satırları (<c>sortOrder</c> sırasında).</summary>
    public IReadOnlyList<InvoiceLineItemResponse> LineItems { get; init; } = [];

    /// <summary>Kaydedilmiş ödemeler (<c>paidAt</c> sırasında).</summary>
    public IReadOnlyList<InvoicePaymentResponse> Payments { get; init; } = [];

    /// <summary>
    /// Denetim izi — append-only, en eskiden yeniye. GoBD §6.3: her işlem kim/ne zaman/ne
    /// bilgisiyle burada görünür.
    /// </summary>
    public IReadOnlyList<InvoiceAuditEntryResponse> AuditTrail { get; init; } = [];
}
