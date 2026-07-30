namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// <b>KDV oranına göre ayrıştırılmış matrah</b> (bir satır = bir oran).
/// <para>
/// <b>Hukuki dayanak:</b> UStG §14 Abs. 4 Nr. 8 — "das nach Steuersätzen und einzelnen
/// Steuerbefreiungen aufgeschlüsselte Entgelt" ve "der ... anzuwendende Steuersatz sowie der auf
/// das Entgelt entfallende Steuerbetrag". Yani faturanın tek bir toplam KDV tutarı göstermesi
/// <b>yetmez</b>: %7 konaklama ile %19 ekstra ayrı ayrı gösterilmelidir. Fatura başlığındaki
/// <c>netAmount</c>/<c>vatAmount</c> yalnızca <i>toplamlardır</i> ve bu zorunluluğu karşılamaz.
/// </para>
/// <para>
/// <b>Kurtaxe bu listede YER ALMAZ.</b> Şehir vergisi otelin bedelinin (Entgelt) parçası değildir,
/// belediye adına tahsil edilen bir <i>durchlaufender Posten</i>'dir (UStG §10 Abs. 1 Satz 5) ve
/// KDV matrahına girmez. Fatura başlığında ayrı bir toplam olarak durur
/// (<c>cityTaxAmount</c>). Bir oranı <b>%0</b> olan gerçek bir hizmet satırı olursa listede kendi
/// satırıyla görünür — bu iki durumun karışmaması için ayrıştırma <c>CityTax</c> türünü türe göre
/// dışlar, orana göre değil.
/// </para>
/// <para>
/// Tutarlar satır tutarlarının <b>toplamıdır</b>; yeniden yuvarlama yapılmaz, böylece
/// <c>Σ netAmount == invoice.netAmount</c> ve <c>Σ vatAmount == invoice.vatAmount</c> her zaman
/// birebir tutar (bkz. <see cref="InvoiceAmounts"/> §4).
/// </para>
/// </summary>
public sealed record InvoiceVatBreakdownResponse
{
    /// <summary>Uygulanan KDV oranı, yüzde (DE: <c>7.00</c> / <c>19.00</c>).</summary>
    public decimal VatRate { get; init; }

    /// <summary>Bu orana tabi <b>matrah</b> (Entgelt, KDV hariç).</summary>
    public decimal NetAmount { get; init; }

    /// <summary>Bu matraha düşen KDV tutarı.</summary>
    public decimal VatAmount { get; init; }

    /// <summary>Bu orana ait brüt tutar (<c>netAmount + vatAmount</c>).</summary>
    public decimal GrossAmount { get; init; }
}
