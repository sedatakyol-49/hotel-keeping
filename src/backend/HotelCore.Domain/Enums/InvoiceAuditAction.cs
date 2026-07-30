namespace HotelCore.Domain.Enums;

/// <summary>
/// Fatura denetim izi aksiyonu (append-only, GoBD §6.3).
/// <para>
/// <b>Numaralar append-only'dir:</b> enum veritabanında <b>string</b> olarak saklanır
/// (<c>InvoiceAuditEntryConfiguration</c> → <c>HasConversion&lt;string&gt;()</c>), bu yüzden yeni
/// değerler mevcut satırları etkilemez. Buna rağmen değerler mantıksal sıraya göre <b>yeniden
/// numaralandırılmaz</b>: sayısal değer log/telemetri, olası dış entegrasyon ve derlenmiş
/// istemcilerde görünebilir; sonuna eklemek tek geriye dönük uyumlu yoldur.
/// </para>
/// </summary>
public enum InvoiceAuditAction
{
    /// <summary>Fatura (taslak veya Stornorechnung) oluşturuldu.</summary>
    Created = 0,

    /// <summary>Fatura kesinleşti: numara atandı, içerik kilitlendi.</summary>
    Finalized = 1,

    /// <summary>
    /// Fatura <b>tamamen</b> ödendi (bakiye kapandı, durum <c>Paid</c>).
    /// Kısmi ödeme için <see cref="PaymentRecorded"/> kullanılır.
    /// </summary>
    Paid = 2,

    /// <summary>Fatura iptal edildi (taslak iptali veya Stornorechnung ile).</summary>
    Cancelled = 3,

    /// <summary>
    /// <b>Taslak</b> fatura güncellendi (misafir/kültür değişimi, satır veya tutar değişikliği).
    /// <para>
    /// GoBD açısından taslak henüz <i>belge</i> değildir (numarası yoktur, muhasebeye girmemiştir),
    /// bu yüzden taslak değişikliklerinin ize yazılması <b>zorunlu değil</b>, ancak
    /// <i>Nachvollziehbarkeit</i> (izlenebilirlik) ilkesi açısından güçlü biçimde tavsiye edilir:
    /// bir faturanın hangi tutarla oluşup hangi tutarla kesinleştiği yalnızca
    /// <c>ModifiedAt/ModifiedByUserId</c> ile açıklanamaz (kim neyi değiştirdi kaybolur).
    /// </para>
    /// </summary>
    Updated = 4,

    /// <summary>
    /// Ödeme kaydedildi — <b>kısmi</b> veya tam. Tutar/bakiye ayrıntısı <c>Details</c> alanındadır.
    /// <para>
    /// <see cref="Paid"/>'den ayrılmasının nedeni: "bir ödeme alındı" bir <i>tahsilat olayıdır</i>,
    /// "fatura ödendi" bir <i>durum geçişidir</i>. İkisi aynı aksiyon adını paylaşınca denetim
    /// izinde "bakiye ne zaman kapandı?" sorusu ancak JSON ayrıntısı ayrıştırılarak
    /// cevaplanabilirdi.
    /// </para>
    /// </summary>
    PaymentRecorded = 5
}
