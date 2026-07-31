using HotelCore.Domain.Enums;

namespace HotelCore.Domain.Entities;

/// <summary>
/// §5 DDG (eski §5 TMG) Impressum künyesi — <b>owned type</b> (<c>Hotels</c> tablosunda
/// <c>LegalProfile_*</c> kolonları). <c>GET /api/v1/public/hotels/{slug}/legal</c> ucunun
/// <c>imprint</c> nesnesinin tek kaynağıdır.
/// <para>
/// <b>Hiçbir alan koda gömülmez.</b> Künye müşteri-değişkenidir: işletmeci tüzel kişilik,
/// ticaret sicili, temsilci ve USt-IdNr. otelden otele farklıdır ve yıl içinde değişir. Koda
/// gömülü bir künye, bir müşteride yanlış olduğu anda §5 DDG ihlali (uyarı/ihtar riski) üretir.
/// </para>
/// <para>
/// <b>Neden Hotel üzerinde, HeadOffice'te değil:</b> mevcut karara göre sözleşmenin tarafı ve
/// fatura keseni oteldir (<c>Hotel.TaxNumber</c> kullanılıyor). Zincir/franchise modelinde künye
/// marka düzeyine taşınabilir — architecture-public-booking.md §10, madde 4 bunu açık bir
/// hukuki soru olarak işaretler; değişecek tek yer bu tiptir.
/// </para>
/// <para>
/// <b>USt-IdNr. burada DEĞİL, <c>Hotel.VatId</c>'dedir:</b> vergi kimlikleri faturada da
/// kullanılır (§14 UStG), yani yalnızca Impressum'a ait değildir; künyeye kopyalanması iki
/// doğruluk kaynağı yaratırdı.
/// </para>
/// </summary>
public sealed class HotelLegalProfile
{
    /// <summary>İşletmeci tüzel kişiliğin tam adı (örn. "HotelCore Berlin Betriebs GmbH").</summary>
    public string? LegalEntityName { get; set; }

    /// <summary>Hukuki biçim (GmbH, GmbH &amp; Co. KG, e.K., AG ...).</summary>
    public string? LegalForm { get; set; }

    /// <summary>Temsilci(ler) — "vertreten durch" (örn. "Anna Becker (Geschäftsführerin)").</summary>
    public string? RepresentedBy { get; set; }

    /// <summary>Künye adresi. Otelin fiziksel adresinden farklı olabilir (merkez adresi).</summary>
    public string? AddressLine { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    /// <summary>Künye ülkesi; boşsa otelin ülkesi kullanılır.</summary>
    public Country? Country { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>Ticaret sicil mahkemesi (örn. "Amtsgericht Berlin-Charlottenburg").</summary>
    public string? RegisterCourt { get; set; }

    /// <summary>Ticaret sicil numarası (örn. "HRB 123456").</summary>
    public string? RegisterNumber { get; set; }

    /// <summary>Denetim makamı — yalnızca izne tabi faaliyetlerde doldurulur (§5 Abs. 1 Nr. 3 DDG).</summary>
    public string? SupervisoryAuthority { get; set; }

    /// <summary>
    /// VSBG: işletme bir tüketici tahkim kuruluna <b>katılıyor mu</b>. Varsayılan <c>false</c>
    /// (§36 VSBG: katılmayan işletme de bunu <i>bildirmek</i> zorundadır — sessiz kalmak seçenek
    /// değildir, bu yüzden alan bool'dur, "bilinmiyor" hâli yoktur).
    /// </summary>
    public bool ParticipatesInDisputeResolution { get; set; }

    /// <summary>
    /// AB ODR platformu bağlantısı (Art. 14 Abs. 1 ODR-VO). Sabit bir URL olsa da koda gömülmez:
    /// bağlantı AB tarafında değişebilir ve bazı otellerde ek/farklı kurul bağlantısı gerekir.
    /// </summary>
    public string? OnlineDisputeResolutionUrl { get; set; }

    /// <summary>
    /// Serbest metin uyuşmazlık çözümü bildirimi. Katılım varsa <b>kurulun adı ve adresi</b>
    /// burada yazılır (§36 VSBG bunu zorunlu kılar); yoksa istemci
    /// <see cref="ParticipatesInDisputeResolution"/> değerinden türetilen i18n anahtarını kullanır.
    /// </summary>
    public string? DisputeResolutionNotice { get; set; }
}
