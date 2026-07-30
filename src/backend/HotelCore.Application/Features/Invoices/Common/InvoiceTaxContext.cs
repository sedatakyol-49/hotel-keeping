namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Faturalama sırasında kullanılan otel bağlamı. Vergi oranları <b>koda hardcode edilmez</b>
/// (architecture.md §4.1): her değer <c>Hotel.TaxProfile</c>'dan (owned type) okunur.
/// <para>
/// Domain'in <c>TaxProfile</c> tipi yerine bu kayıt kullanılır çünkü owned type'lar EF Core
/// izdüşümünde (<c>Select(... new TaxProfile { ... })</c>) doğrudan oluşturulamaz; skaler
/// alanları çekmek hem çalışır hem de yalnızca gereken kolonları okur.
/// </para>
/// </summary>
/// <param name="HotelId">Faturanın oteli.</param>
/// <param name="Currency">Otelin para birimi — faturaya buradan yazılır, istemciden alınmaz.</param>
/// <param name="DefaultCulture">Otelin varsayılan dili (fatura dili için son çare).</param>
/// <param name="VatRate">Standart KDV oranı, yüzde (DE: 19).</param>
/// <param name="ReducedVatRate">İndirimli KDV oranı, yüzde (DE: 7 — konaklama).</param>
/// <param name="CityTaxPerPersonNight">Kurtaxe: kişi başı gecelik tutar.</param>
/// <param name="CityTaxEnabled">Bu otelde şehir vergisi uygulanıyor mu.</param>
internal sealed record InvoiceTaxContext(
    Guid HotelId,
    string Currency,
    string DefaultCulture,
    decimal VatRate,
    decimal ReducedVatRate,
    decimal CityTaxPerPersonNight,
    bool CityTaxEnabled);
