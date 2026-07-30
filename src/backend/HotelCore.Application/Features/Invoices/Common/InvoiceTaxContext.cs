using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Faturalama sırasında kullanılan otel bağlamı. Vergi oranları <b>koda hardcode edilmez</b>
/// (architecture.md §4.1): her değer <c>Hotel.TaxProfile</c>'dan (owned type) okunur.
/// <para>
/// Domain'in <c>TaxProfile</c> tipi yerine bu kayıt kullanılır çünkü owned type'lar EF Core
/// izdüşümünde (<c>Select(... new TaxProfile { ... })</c>) doğrudan oluşturulamaz; skaler
/// alanları çekmek hem çalışır hem de yalnızca gereken kolonları okur.
/// </para>
/// <para>
/// <b>Kural taşımaz:</b> vergi <i>hesabına</i> giren kararlar (örn. Kurtaxe'ye tabi kişi kümesi)
/// burada yeniden yorumlanmaz; <see cref="ToTaxProfile"/> ile domain nesnesine dönülüp
/// <see cref="TaxProfile.CountTaxablePersons"/> çağrılır — kuralın tek sahibi Domain'dir.
/// </para>
/// </summary>
/// <param name="HotelId">Faturanın oteli.</param>
/// <param name="Currency">Otelin para birimi — faturaya buradan yazılır, istemciden alınmaz.</param>
/// <param name="DefaultCulture">Otelin varsayılan dili (fatura dili için son çare).</param>
/// <param name="VatRate">Standart KDV oranı, yüzde (DE: 19).</param>
/// <param name="ReducedVatRate">İndirimli KDV oranı, yüzde (DE: 7 — konaklama).</param>
/// <param name="CityTaxPerPersonNight">Kurtaxe: kişi başı gecelik tutar.</param>
/// <param name="CityTaxEnabled">Bu otelde şehir vergisi uygulanıyor mu.</param>
/// <param name="CityTaxExemptChildren">
/// Kurtaxe'de çocuklar muaf mı — <c>true</c> ise vergiye tabi kişi sayısı yalnızca yetişkinlerdir.
/// </param>
/// <param name="CityTaxChildAgeLimit">
/// Muafiyetin yaş sınırı (varsa). <b>Hesaba girmez</b> (rezervasyonda doğum tarihi yoktur);
/// yalnızca satır açıklamasında muafiyetin dayanağı olarak yazdırılır.
/// </param>
internal sealed record InvoiceTaxContext(
    Guid HotelId,
    string Currency,
    string DefaultCulture,
    decimal VatRate,
    decimal ReducedVatRate,
    decimal CityTaxPerPersonNight,
    bool CityTaxEnabled,
    bool CityTaxExemptChildren,
    int? CityTaxChildAgeLimit)
{
    /// <summary>
    /// Bağlamı domain vergi profiline çevirir. Yalnızca <b>bellekte</b> kullanılır (veritabanına
    /// yazılmaz): amaç, vergi kurallarını içeren domain metotlarını çağırabilmektir.
    /// </summary>
    public TaxProfile ToTaxProfile() =>
        new()
        {
            VatRate = VatRate,
            ReducedVatRate = ReducedVatRate,
            CityTaxPerPersonNight = CityTaxPerPersonNight,
            CityTaxEnabled = CityTaxEnabled,
            CityTaxExemptChildren = CityTaxExemptChildren,
            CityTaxChildAgeLimit = CityTaxChildAgeLimit
        };
}
