namespace HotelCore.Domain.Entities;

/// <summary>
/// Otelin vergi profili — <b>owned type</b> (Hotel tablosunda kolon olarak saklanır).
/// Oranlar koda hardcode EDİLMEZ; otel bazında admin panelden yönetilir (architecture.md §4.1).
/// </summary>
public sealed class TaxProfile
{
    /// <summary>Standart KDV oranı, yüzde (DE: 19).</summary>
    public decimal VatRate { get; set; }

    /// <summary>İndirimli KDV oranı, yüzde (DE: 7 — konaklama).</summary>
    public decimal ReducedVatRate { get; set; }

    /// <summary>Kurtaxe: kişi başı / gece şehir vergisi tutarı.</summary>
    public decimal CityTaxPerPersonNight { get; set; }

    /// <summary>Şehir vergisi bu otelde uygulanıyor mu.</summary>
    public bool CityTaxEnabled { get; set; }

    /// <summary>
    /// Kurtaxe hesabında <b>çocuklar muaf mı</b> (Almanya'da birçok belediyede reşit olmayanlar
    /// şehir vergisinden muaftır). <c>true</c> ise vergiye tabi kişi sayısı
    /// <c>Reservation.Adults</c>'tır; <c>false</c> ise <c>Adults + Children</c>.
    /// <para>
    /// <b>Neden ayrı bir bool:</b> muafiyetin <i>var olup olmadığı</i> hesaplanabilir tek bilgidir.
    /// <c>Reservation</c> yalnızca <c>Adults</c>/<c>Children</c> <b>sayısını</b> tutar, misafirlerin
    /// doğum tarihi yoktur; bu yüzden yaş sınırı çalışma zamanında uygulanamaz — muafiyet ancak
    /// "çocuk olarak girilen kişiler" kümesine uygulanabilir.
    /// </para>
    /// <para>
    /// <b>Varsayılan <c>false</c> bilinçlidir:</b> mevcut hesap (<c>(Adults + Children) × gece</c>)
    /// korunur; muafiyet yalnızca otel açıkça açtığında devreye girer (geriye dönük sürpriz olmaz).
    /// </para>
    /// </summary>
    public bool CityTaxExemptChildren { get; set; }

    /// <summary>
    /// Muafiyetin geçerli olduğu <b>yaş sınırı</b> (bu yaşın altındakiler muaf; DE'de tipik olarak
    /// 18, bazı belediyelerde 16/14/6). Belirtilmemişse null.
    /// <para>
    /// <b>Neden bool yetmiyor:</b> sınır belediyeye göre değişir ve iki yerde <i>gerekir</i>:
    /// (1) faturada/Kurtaxe beyanında muafiyetin hukuki dayanağı olarak yazdırılır
    /// ("Kinder unter 18 Jahren sind von der Kurtaxe befreit"), (2) resepsiyonun bir misafiri
    /// <c>Children</c> olarak sayıp saymayacağının <b>tek</b> operasyonel tanımıdır — sınır
    /// saklanmazsa "çocuk" kavramı otel içinde belgesiz kalır.
    /// <b>Hesaplamada kullanılmaz</b> (doğum tarihi yok); yaşa göre gerçek ayrıştırma ancak
    /// misafir başına yaş/doğum tarihi modellendiğinde mümkün olur.
    /// </para>
    /// </summary>
    public int? CityTaxChildAgeLimit { get; set; }

    /// <summary>
    /// Kurtaxe'ye <b>tabi kişi sayısı</b>. Vergiye tabi kişi kümesi bir vergi profili kuralıdır,
    /// bu yüzden hesabın bu parçası domain'de tek noktada tutulur (Application katmanı
    /// <c>adults + children</c> toplamını kendi başına yorumlamasın).
    /// </summary>
    /// <param name="adults">Yetişkin sayısı.</param>
    /// <param name="children">Çocuk sayısı (rezervasyonda girilen sayı; yaş bilgisi yoktur).</param>
    public int CountTaxablePersons(int adults, int children)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(adults);
        ArgumentOutOfRangeException.ThrowIfNegative(children);

        return CityTaxExemptChildren ? adults : adults + children;
    }
}
