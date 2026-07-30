namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura detayı: liste alanları + satırlar + ödemeler + <b>denetim izi</b> (GoBD §6.3) +
/// <b>§14 UStG zorunlu belge içeriği</b> (düzenleyen, alıcı, hizmet tarihi, oran bazında matrah).
/// <para>
/// Liste tipinden türetilmez (kalıtım OpenAPI'de <c>allOf</c> üretir ve frontend client
/// üretimini karmaşıklaştırır); alanlar bilinçli olarak tekrarlanır.
/// </para>
/// <para>
/// <b>Neden bu tip §14'ü taşımak zorunda:</b> belge (PDF/ZUGFeRD) üretimi bu fazda yok, ama
/// üretici katman veriyi <i>buradan</i> alacak. Veri yanıtta eksikse belge geldiğinde de eksik
/// olur — asıl mesele budur. Türetilebilen her §14 alanı burada döner; türetilemeyenler
/// (alıcı adresinin ülkesi, önceden kararlaştırılmış indirim, belge anındaki künyenin
/// dondurulması) <b>şema ihtiyacı</b> olarak raporlanmıştır ve burada <b>uydurulmaz</b>.
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

    /// <summary>
    /// Düzenleyenin künyesi — UStG §14 Abs. 4 Nr. 1 ve Nr. 2 (ad/adres + Steuernummer/USt-IdNr.).
    /// </summary>
    public InvoiceIssuerResponse Issuer { get; init; } = new();

    /// <summary>Alıcının adı/adresi — UStG §14 Abs. 4 Nr. 1.</summary>
    public InvoiceRecipientResponse Recipient { get; init; } = new();

    /// <summary>
    /// Hizmetin verildiği dönemin <b>başlangıcı</b>.
    /// <para>
    /// <b>Hukuki dayanak:</b> UStG §14 Abs. 4 Nr. 6 — <i>der Zeitpunkt der Lieferung oder sonstigen
    /// Leistung</i>. Konaklamada bu tek bir gün değil bir <b>aralıktır</b> (Anreise → Abreise).
    /// </para>
    /// <para>
    /// <b>Kaynak:</b> rezervasyona bağlı faturada aralık rezervasyonun <c>checkIn</c>/<c>checkOut</c>
    /// tarihlerinden gelir ve satırların <c>serviceDate</c> değerleriyle <b>genişletilir</b> (ör.
    /// çıkış günü kaydedilen bir ekstra). Elle kesilen faturada yalnızca satır tarihleri kullanılır.
    /// Hiçbir kaynak yoksa <c>null</c>.
    /// </para>
    /// <para>
    /// <b>Neden satır tarihleri tek başına yetmiyor:</b> konaklama satırının <c>serviceDate</c>'i
    /// yalnızca <b>giriş günüdür</b>; 2 gecelik bir konaklama belgede tek güne indirgenirdi.
    /// </para>
    /// </summary>
    public DateOnly? ServicePeriodFrom { get; init; }

    /// <summary>Hizmet döneminin <b>sonu</b> (konaklamada çıkış günü) — bkz. <see cref="ServicePeriodFrom"/>.</summary>
    public DateOnly? ServicePeriodTo { get; init; }

    /// <summary>
    /// KDV oranına göre ayrıştırılmış matrah ve vergi — UStG §14 Abs. 4 Nr. 8. Orana göre artan
    /// sırada; Kurtaxe satırları <b>dâhil değildir</b> (durchlaufender Posten).
    /// </summary>
    public IReadOnlyList<InvoiceVatBreakdownResponse> VatBreakdown { get; init; } = [];

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
