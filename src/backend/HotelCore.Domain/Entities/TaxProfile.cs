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
}
