namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Fatura belgesi üretici portu (architecture.md §6.5 — makine-okunabilirlik zemini).
/// <para>
/// <b>Bu fazda implementasyonu YOKTUR</b> ve DI'a kayıtlı değildir: PDF üretimi (QuestPDF vb.)
/// ve ZUGFeRD/XRechnung XML gömme ayrı bir paket kararı gerektirir. Port şimdiden tanımlanır ki
/// <c>GET /api/v1/invoices/{id}/pdf</c> uç noktası sözleşmede yerini alsın ve bugün
/// <b>501 Not Implemented</b> dönsün — sahte/boş bir PDF döndürmek denetim açısından
/// yanıltıcı olurdu.
/// </para>
/// <para>
/// <b>GoBD notu:</b> belge üretimi <i>türetilmiş</i> bir çıktıdır; yapılandırılmış veri
/// (Invoice + satırlar + denetim izi) her hâlükârda veritabanında saklanır. Bu yüzden PDF'in
/// olmaması saklama yükümlülüğünü ihlal etmez, ancak <b>ZUGFeRD/XRechnung</b> (§6.5) canlıya
/// çıkmadan önce tamamlanmalıdır.
/// </para>
/// </summary>
public interface IInvoiceExporter
{
    /// <summary>Bu üreticinin desteklediği biçimler.</summary>
    IReadOnlyCollection<InvoiceExportFormat> SupportedFormats { get; }

    /// <summary>
    /// Kesinleşmiş bir faturayı istenen biçimde üretir. Taslak fatura için çağrılmamalıdır
    /// (belge işlevi finalize ile başlar).
    /// </summary>
    Task<InvoiceExportDocument> ExportAsync(
        Guid invoiceId,
        InvoiceExportFormat format,
        CancellationToken cancellationToken);
}

/// <summary>Fatura çıktı biçimleri.</summary>
public enum InvoiceExportFormat
{
    /// <summary>İnsan-okunur PDF (görsel düzen).</summary>
    Pdf = 0,

    /// <summary>PDF/A-3 + gömülü ZUGFeRD XML (hibrit e-fatura).</summary>
    ZugferdPdfA3 = 1,

    /// <summary>Saf XRechnung XML (kamu alıcıları için).</summary>
    XRechnungXml = 2
}

/// <summary>Üretilen belge.</summary>
/// <param name="Content">Belge içeriği.</param>
/// <param name="ContentType">MIME tipi (örn. <c>application/pdf</c>).</param>
/// <param name="FileName">İndirme adı (örn. <c>2026-000001.pdf</c>).</param>
public sealed record InvoiceExportDocument(byte[] Content, string ContentType, string FileName);
