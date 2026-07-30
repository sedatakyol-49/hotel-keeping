namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura listesi öğesi — docs/api-contracts-invoices.md ile birebir.
/// Satır/ödeme/denetim izi <b>içermez</b>; onlar detay uç noktasında döner.
/// </summary>
public sealed record InvoiceResponse
{
    public Guid Id { get; init; }

    /// <summary>
    /// Fatura numarası. <b>Taslakta null</b>: numara yalnızca finalize anında atanır
    /// (GoBD §6.2 — boşluksuz sekans).
    /// </summary>
    public string? InvoiceNumber { get; init; }

    /// <summary>Durum enum <b>adı</b>: <c>Draft | Finalized | Paid | Cancelled</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Fatura tarihi — finalize anında damgalanır, taslakta null.</summary>
    public DateTimeOffset? IssuedAt { get; init; }

    public Guid GuestId { get; init; }

    public string GuestName { get; init; } = string.Empty;

    public Guid? ReservationId { get; init; }

    public string? ReservationNumber { get; init; }

    public string Culture { get; init; } = string.Empty;

    public string Currency { get; init; } = string.Empty;

    /// <summary>KDV'li satırların net toplamı (Kurtaxe hariç).</summary>
    public decimal NetAmount { get; init; }

    public decimal VatAmount { get; init; }

    /// <summary>Kurtaxe toplamı — KDV matrahına dâhil değildir.</summary>
    public decimal CityTaxAmount { get; init; }

    /// <summary>Ödenecek toplam = net + KDV + Kurtaxe.</summary>
    public decimal GrossAmount { get; init; }

    /// <summary>Kaydedilmiş ödemelerin toplamı.</summary>
    public decimal PaidAmount { get; init; }

    /// <summary>Kalan bakiye = brüt − ödenen (sunucuda hesaplanır).</summary>
    public decimal OutstandingAmount { get; init; }

    /// <summary>Bu faturayı iptal eden Stornorechnung (varsa) — GoBD §6.1.</summary>
    public Guid? CancelledByInvoiceId { get; init; }

    /// <summary>Bu fatura bir iptal faturasıysa, iptal ettiği orijinal faturanın kimliği.</summary>
    public Guid? CancelsInvoiceId { get; init; }

    /// <summary>Kolaylık alanı: <c>true</c> ise bu belge bir Stornorechnung'dur.</summary>
    public bool IsCancellationInvoice { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
