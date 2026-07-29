using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// Fatura (Rechnung). GoBD gereği <see cref="InvoiceStatus.Finalized"/> sonrası içerik
/// değiştirilemez (architecture.md §6.1); durum geçişleri yalnızca aşağıdaki domain
/// metotlarıyla yapılır ve <c>AppDbContext.SaveChangesAsync</c> içindeki guard ile ikinci
/// kez doğrulanır. Fatura hard-delete edilmez (10 yıl saklama → soft-delete).
/// </summary>
public sealed class Invoice : EntityBase, ITenantEntity, IAuditableEntity, ISoftDeletable
{
    public Guid HotelId { get; set; }

    public Hotel Hotel { get; set; } = null!;

    /// <summary>
    /// Otel bazında boşluksuz artan numara (<see cref="HotelInvoiceCounter"/> ile üretilir).
    /// Taslak faturada boş olabilir; finalize sırasında atanır.
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid? ReservationId { get; set; }

    public Reservation? Reservation { get; set; }

    public Guid GuestId { get; set; }

    public Guest Guest { get; set; } = null!;

    /// <summary>Fatura tarihi — finalize anında set edilir.</summary>
    public DateTimeOffset? IssuedAt { get; private set; }

    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>Faturanın basıldığı dil (de/en/tr).</summary>
    public string Culture { get; set; } = "de";

    public string Currency { get; set; } = "EUR";

    public decimal NetAmount { get; set; }

    public decimal VatAmount { get; set; }

    /// <summary>Kurtaxe toplamı (KDV dışı kalem).</summary>
    public decimal CityTaxAmount { get; set; }

    public decimal GrossAmount { get; set; }

    /// <summary>Bu faturayı iptal eden Stornorechnung kaydının kimliği (varsa).</summary>
    public Guid? CancelledByInvoiceId { get; private set; }

    public Invoice? CancelledByInvoice { get; set; }

    public ICollection<InvoiceLineItem> LineItems { get; } = [];

    public ICollection<Payment> Payments { get; } = [];

    public ICollection<InvoiceAuditEntry> AuditEntries { get; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset? ModifiedAt { get; set; }

    public Guid? ModifiedByUserId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Taslak fatura kesinleşir: numara atanır, tarih yazılır, içerik kilitlenir.</summary>
    public void MarkFinalized(string invoiceNumber, DateTimeOffset issuedAt)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException($"Yalnizca taslak fatura finalize edilebilir. Mevcut durum: {Status}.");
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new ArgumentException("Fatura numarasi bos olamaz.", nameof(invoiceNumber));
        }

        InvoiceNumber = invoiceNumber;
        IssuedAt = issuedAt;
        Status = InvoiceStatus.Finalized;
    }

    /// <summary>Ödeme tamamlandı olarak işaretler.</summary>
    public void MarkPaid()
    {
        if (Status != InvoiceStatus.Finalized)
        {
            throw new InvalidOperationException($"Yalnizca kesinlesmis fatura ödendi olarak isaretlenebilir. Mevcut durum: {Status}.");
        }

        Status = InvoiceStatus.Paid;
    }

    /// <summary>
    /// Faturayı iptal eder. Kesinleşmiş fatura silinmez/düzeltilmez; iptal faturası
    /// (Stornorechnung) oluşturulur ve orijinal ona bağlanır.
    /// </summary>
    public void MarkCancelled(Guid? cancellationInvoiceId = null)
    {
        if (Status == InvoiceStatus.Cancelled)
        {
            throw new InvalidOperationException("Fatura zaten iptal edilmis.");
        }

        if (Status != InvoiceStatus.Draft && cancellationInvoiceId is null)
        {
            throw new InvalidOperationException("Kesinlesmis bir fatura ancak iptal faturasi (Stornorechnung) ile iptal edilebilir.");
        }

        CancelledByInvoiceId = cancellationInvoiceId;
        Status = InvoiceStatus.Cancelled;
    }
}
