using System.Linq.Expressions;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// <b>"Bu fatura hâlâ yürürlükte mi?"</b> sorusunun <b>tek</b> tanımı. Fatura modülü de raporlama
/// modülü de aynı cümleyi kullanır; tanım iki yerde ayrı yazıldığında ikisi kaçınılmaz olarak
/// ayrışır (gerçekleşen hata: rezervasyon "faturalanmış" sayılıp bir daha faturalanamıyor, tutarı
/// da raporda hiçbir kovaya girmiyordu).
///
/// <para><b>Kural:</b> bir fatura, <i>iptal edilmemişse</i> <b>ve</b> <i>kendisi bir
/// Stornorechnung değilse</i> yürürlüktedir.</para>
///
/// <para><b>Neden storno'yu dışlamak şart:</b> iptal faturası GoBD gereği orijinalin bir
/// <i>aynasıdır</i> ve orijinalin taşıdığı alanları taşır — <c>ReservationId</c> dâhil
/// (<c>CancelInvoiceHandler</c>). Üstelik kendisi <b>numara alır</b> ve durumu
/// <c>Finalized</c>'dır. Yani "iptal edilmemiş fatura" testi storno'yu <b>yürürlükteki fatura</b>
/// sanır: orijinal iptal edilip storno kesildikten sonra rezervasyon <i>kalıcı olarak</i>
/// faturalanamaz hâle gelirdi (409), üstelik hatanın kendi metni "düzeltmek için storno
/// oluşturun" der. Storno'yu <c>CancelsInvoiceId</c> üzerinden tanımak doğru ayrımdır: bu kolon
/// yalnızca iptal faturalarında doludur ve çift domain'de kurulur.</para>
///
/// <para><b>Neden bu tanım "net etki" ile aynı şey:</b> orijinal (+X) ile storno (−X) her zaman
/// birlikte var olur ve toplamları <b>tam sıfırdır</b>. İkisini birden eleyen bu kural, muhasebe
/// açısından "bu rezervasyondan hâlâ tahsil edilecek bir belge var mı?" sorusuna cevap verir.</para>
/// </summary>
internal static class InvoiceEffectiveness
{
    /// <summary>
    /// Yürürlükteki fatura: iptal edilmemiş ve kendisi iptal faturası (Stornorechnung) olmayan.
    /// <b>Taslaklar dâhildir</b> — henüz belge olmasa da rezervasyon üzerinde açık bir fatura
    /// vardır ve ikincisi kesilmemelidir.
    /// </summary>
    public static readonly Expression<Func<Invoice, bool>> IsEffective =
        invoice => invoice.Status != InvoiceStatus.Cancelled && invoice.CancelsInvoiceId == null;

    /// <summary>
    /// Yürürlükteki <b>belge</b>: yukarıdaki kurala ek olarak bir kez numara almış
    /// (<c>IssuedAt != null</c>) fatura. Taslak belge değildir, terk edilebilir; bu yüzden
    /// muhasebe/raporlama tarafı bu daha dar kümeyi kullanır (bkz. <c>RevenueRecognition</c>).
    /// </summary>
    public static readonly Expression<Func<Invoice, bool>> IsEffectiveDocument =
        invoice => invoice.Status != InvoiceStatus.Cancelled
                   && invoice.CancelsInvoiceId == null
                   && invoice.IssuedAt != null;

    /// <summary>Sorguyu yürürlükteki faturalarla sınırlar (<see cref="IsEffective"/>).</summary>
    public static IQueryable<Invoice> Effective(this IQueryable<Invoice> invoices)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        return invoices.Where(IsEffective);
    }

    /// <summary>
    /// Sorguyu yürürlükteki <b>belgelerle</b> sınırlar (<see cref="IsEffectiveDocument"/>).
    /// </summary>
    public static IQueryable<Invoice> EffectiveDocuments(this IQueryable<Invoice> invoices)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        return invoices.Where(IsEffectiveDocument);
    }
}
