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

    /// <summary>
    /// Bu fatura bir <b>Stornorechnung</b> ise iptal ettiği orijinal faturanın kimliği; değilse null.
    /// <para>
    /// <see cref="CancelledByInvoiceId"/>'nin ters yönüdür. İki alan birlikte GoBD storno çiftini
    /// <b>her iki yönden</b> okunabilir yapar: "bu belge iptal edildi mi?" ve "bu belge neyi iptal
    /// ediyor?". Ters yön saklanmazsa ikinci soru ancak ilintili alt sorguyla (her satır için
    /// <c>Invoices</c> taraması) cevaplanabilir.
    /// </para>
    /// </summary>
    public Guid? CancelsInvoiceId { get; private set; }

    public Invoice? CancelsInvoice { get; set; }

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
    /// <para>
    /// <b>Not:</b> bu aşırı yükleme yalnızca <b>ileri</b> yönü (<see cref="CancelledByInvoiceId"/>)
    /// yazabilir; storno nesnesine erişimi olmadığı için geri referansı
    /// (<see cref="CancelsInvoiceId"/>) kuramaz. Storno nesnesi elinizdeyse
    /// <see cref="MarkCancelled(Invoice)"/> aşırı yüklemesini kullanın — çifti tek çağrıda ve
    /// tutarlı biçimde kurar. Bu yol kullanıldığında geri referansı <c>AppDbContext</c> kaydetme
    /// sırasında tamamlar (yalnızca güvenlik ağı; birincil yol domain metodudur).
    /// </para>
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

        if (cancellationInvoiceId == Id)
        {
            throw new InvalidOperationException("Bir fatura kendisini iptal edemez.");
        }

        CancelledByInvoiceId = cancellationInvoiceId;
        Status = InvoiceStatus.Cancelled;
    }

    /// <summary>
    /// Faturayı verilen <b>Stornorechnung</b> ile iptal eder ve <b>çiftin iki yönünü birlikte</b>
    /// kurar: <c>orijinal.CancelledByInvoiceId = storno.Id</c> ve
    /// <c>storno.CancelsInvoiceId = orijinal.Id</c>.
    /// <para>
    /// Değişmez (invariant) burada korunur çünkü iki kolonun eşleşmesi <b>satır içi</b> bir kural
    /// değildir: PostgreSQL'de karşılıklı iki FK'nin birbirini işaret ettiğini doğrulayan bir
    /// bildirimsel kısıt yoktur (CHECK yalnızca aynı satırı görür). Bu yüzden değişmezin sahibi
    /// domain metodudur; veritabanı tarafında yalnızca <b>satır içi</b> kısım
    /// (kendini iptal etme yasağı) CHECK ile garanti edilir.
    /// </para>
    /// </summary>
    public void MarkCancelled(Invoice cancellationInvoice)
    {
        ArgumentNullException.ThrowIfNull(cancellationInvoice);

        if (ReferenceEquals(cancellationInvoice, this))
        {
            throw new InvalidOperationException("Bir fatura kendisini iptal edemez.");
        }

        if (cancellationInvoice.CancelsInvoiceId is Guid existing && existing != Id)
        {
            throw new InvalidOperationException(
                $"Bu iptal faturasi baska bir faturayi iptal ediyor (CancelsInvoiceId: {existing}); " +
                "bir Stornorechnung yalnizca tek bir faturayi iptal edebilir.");
        }

        // Sira onemli: once durum gecisi dogrulanir (hata firlatirsa storno'ya dokunulmaz).
        MarkCancelled(cancellationInvoice.Id);
        cancellationInvoice.CancelsInvoiceId = Id;
    }

    /// <summary>
    /// Geri referansı (<see cref="CancelsInvoiceId"/>) tamamlar. Yalnızca <b>persistence
    /// katmanının tutarlılık güvenlik ağı</b> için vardır: ileri yön
    /// <see cref="MarkCancelled(Guid?)"/> ile yazıldığında çiftin ikinci yarısını kaydetme
    /// sırasında doldurur. Zaten aynı değere sahipse işlem yapmaz; farklı bir faturayı işaret
    /// ediyorsa hata verir.
    /// </summary>
    public void LinkCancelledInvoice(Guid cancelledInvoiceId)
    {
        if (cancelledInvoiceId == Id)
        {
            throw new InvalidOperationException("Bir fatura kendisini iptal edemez.");
        }

        if (CancelsInvoiceId is Guid existing)
        {
            if (existing != cancelledInvoiceId)
            {
                throw new InvalidOperationException(
                    $"Iptal faturasi zaten {existing} kimlikli faturayi iptal ediyor; " +
                    $"{cancelledInvoiceId} ile yeniden baglanamaz.");
            }

            return;
        }

        CancelsInvoiceId = cancelledInvoiceId;
    }
}
