using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Invoices.Common;

/// <summary>
/// Fatura tutar matematiğinin <b>tek</b> yeri. Tüm tutarlar sunucuda hesaplanır; istemciden gelen
/// toplamlara (net/KDV/brüt) <b>asla güvenilmez</b> — istemci yalnızca miktar ve birim fiyat
/// gönderir, KDV oranı bile <c>Hotel.TaxProfile</c>'dan çözülür.
///
/// <para><b>1) Fiyat tabanı: birim fiyatlar BRÜTtür (KDV dâhil).</b>
/// Gerekçe: (a) Almanya'da tüketiciye gösterilen fiyat brüt son fiyattır (PAngV), (b) Domain'de
/// <c>Reservation.TotalAmount</c> "toplam brüt tutar" olarak tanımlıdır — oda ücretini net
/// saymak faturayı rezervasyon tutarıyla uzlaştırılamaz hâle getirirdi, (c) Kurtaxe zaten sabit
/// brüt bir tutardır. KDV satır bazında <i>içinden çıkarılır</i> (herausgerechnet):
/// <c>net = brüt / (1 + oran)</c>, <c>kdv = brüt − net</c>. Bu, <c>net + kdv</c> toplamının
/// yazdırılan satır tutarına <b>kuruş kuruş eşit</b> olmasını garanti eder.
/// <b>Ürün kararı:</b> fiyatların net kabul edilmesi istenirse değişecek tek yer
/// <see cref="ComputeLine"/>'dır.</para>
///
/// <para><b>2) KDV oranı eşlemesi</b> (<see cref="ResolveVatRate"/>):
/// <list type="bullet">
///   <item><see cref="InvoiceLineType.RoomCharge"/> → <c>TaxProfile.ReducedVatRate</c>
///   (DE: %7 — konaklama hizmeti, UStG §12 Abs. 2 Nr. 11).</item>
///   <item><see cref="InvoiceLineType.Extra"/> → <c>TaxProfile.VatRate</c>
///   (DE: %19 — kahvaltı, minibar, otopark gibi konaklamaya dâhil olmayan hizmetler;
///   "Aufteilungsgebot" gereği kahvaltı indirimli orandan YARARLANMAZ).</item>
///   <item><see cref="InvoiceLineType.CityTax"/> → <b>%0</b> (bkz. 3).</item>
/// </list></para>
///
/// <para><b>3) Kurtaxe neden KDV'siz:</b> Kurtaxe/Übernachtungsteuer belediyenin <i>misafirden</i>
/// aldığı bir yerel vergidir; otel onu yalnızca tahsil edip belediyeye aktarır (durchlaufender
/// Posten) — otelin kendi hizmet bedelinin (Entgelt) parçası değildir, dolayısıyla KDV matrahına
/// girmez. Faturada ayrı bir toplam olarak gösterilir (<c>Invoice.CityTaxAmount</c>) ve
/// <c>NetAmount</c>'a dâhil EDİLMEZ. <b>Uyarı / ürün kararı:</b> bazı şehirlerin
/// "Bettensteuer" uygulamalarında vergi idaresi bunu bedelin parçası sayabilir; belediye bazında
/// mali müşavir onayı gerekir. Model bunu destekliyor: oran ve açma/kapama otel bazında
/// <c>TaxProfile</c>'dadır, gerekirse <c>CityTax</c> satırına oran verilebilecek şekilde
/// tek noktadan değiştirilir.</para>
///
/// <para><b>4) Yuvarlama:</b> ondalık <b>2</b> hane, <b>kaufmännisch</b> (yarım yukarı,
/// <see cref="MidpointRounding.AwayFromZero"/>) ve <b>satır bazında</b>. Toplamlar yuvarlanmış
/// satır tutarlarının toplamıdır (toplamı sonradan yeniden yuvarlamak yazdırılan satırlarla
/// toplam arasında kuruş farkı doğururdu). Negatif tutarlarda (Stornorechnung)
/// <c>AwayFromZero</c> simetriktir, bu yüzden iptal faturası orijinali <b>tam olarak</b> sıfırlar.</para>
/// </summary>
internal static class InvoiceAmounts
{
    /// <summary>Para alanlarının ondalık hane sayısı (DB kolonları da <c>decimal(18,2)</c>).</summary>
    public const int MoneyScale = 2;

    /// <summary>Ticari yuvarlama (yarım yukarı) — DE fatura uygulamasında beklenen davranış.</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    /// <summary>Satır türüne göre KDV oranını otelin vergi profilinden çözer.</summary>
    public static decimal ResolveVatRate(InvoiceLineType type, InvoiceTaxContext tax)
    {
        ArgumentNullException.ThrowIfNull(tax);

        return type switch
        {
            InvoiceLineType.RoomCharge => tax.ReducedVatRate,
            InvoiceLineType.CityTax => 0m,
            _ => tax.VatRate
        };
    }

    /// <summary>
    /// Satır tutarlarını hesaplar. <paramref name="grossUnitPrice"/> KDV dâhil birim fiyattır.
    /// </summary>
    public static LineAmounts ComputeLine(decimal quantity, decimal grossUnitPrice, decimal vatRate)
    {
        var gross = Round(quantity * grossUnitPrice);

        if (vatRate <= 0m)
        {
            // KDV'siz satır (Kurtaxe veya oranı %0 tanımlı otel): tamamı net kabul edilir.
            return new LineAmounts(gross, 0m, gross);
        }

        var net = Round(gross / (1m + (vatRate / 100m)));

        // KDV artık kalandır: net + kdv == brüt her zaman doğrudur (kuruş kaçağı olmaz).
        return new LineAmounts(net, gross - net, gross);
    }

    /// <summary>Satır tutarlarını entity'ye yazar (tek yerden, böylece kolon ikilisi tutarlı kalır).</summary>
    public static void ApplyLineAmounts(InvoiceLineItem line, decimal vatRate)
    {
        ArgumentNullException.ThrowIfNull(line);

        var amounts = ComputeLine(line.Quantity, line.UnitPrice, vatRate);

        line.VatRate = vatRate;
        line.LineNet = amounts.Net;
        line.LineVat = amounts.Vat;
    }

    /// <summary>
    /// Satır tutarlarını <b>verilen brüt tutardan</b> yazar — <c>miktar × birim fiyat</c> çarpımı
    /// yeniden yapılmaz.
    /// <para>
    /// <b>Neden gerekli:</b> konaklama gece gece fiyatlanır
    /// (<c>ReservationPricingService</c>: sezon geçişinde geceler farklı planlara düşebilir), bu
    /// yüzden brüt <b>toplam</b> otoriter değerdir; birim fiyat yalnızca gösterim amaçlı bir
    /// ortalamadır. Örnek: 3 gece / 250,00 → birim fiyat 83,33 ve çarpım 249,99 ederdi; fatura
    /// böylece <c>Reservation.TotalAmount</c> ile uzlaştırılamaz hâle gelir ve 1 kuruş kaçardı.
    /// </para>
    /// Yuvarlama ve "KDV = brüt − net" kalanı kuralı <see cref="ComputeLine"/> ile aynıdır.
    /// </summary>
    public static void ApplyLineAmountsFromGross(InvoiceLineItem line, decimal gross, decimal vatRate)
    {
        ArgumentNullException.ThrowIfNull(line);

        var rounded = Round(gross);

        line.VatRate = vatRate;

        if (vatRate <= 0m)
        {
            line.LineNet = rounded;
            line.LineVat = 0m;

            return;
        }

        var net = Round(rounded / (1m + (vatRate / 100m)));

        line.LineNet = net;
        line.LineVat = rounded - net;
    }

    /// <summary>
    /// Fatura toplamlarını satırlardan hesaplar.
    /// <c>NetAmount</c> = KDV'li satırların net toplamı, <c>CityTaxAmount</c> = Kurtaxe satırları,
    /// <c>GrossAmount</c> = net + KDV + Kurtaxe.
    /// </summary>
    public static InvoiceTotals ComputeTotals(IEnumerable<InvoiceLineItem> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var net = 0m;
        var vat = 0m;
        var cityTax = 0m;

        foreach (var line in lines)
        {
            if (line.Type is InvoiceLineType.CityTax)
            {
                cityTax += line.LineNet;
                continue;
            }

            net += line.LineNet;
            vat += line.LineVat;
        }

        return new InvoiceTotals(net, vat, cityTax, net + vat + cityTax);
    }

    /// <summary>Toplamları faturaya yazar.</summary>
    public static void ApplyTotals(Invoice invoice, IEnumerable<InvoiceLineItem> lines)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var totals = ComputeTotals(lines);

        invoice.NetAmount = totals.Net;
        invoice.VatAmount = totals.Vat;
        invoice.CityTaxAmount = totals.CityTax;
        invoice.GrossAmount = totals.Gross;
    }
}

/// <summary>Tek satırın tutarları.</summary>
/// <param name="Net">KDV hariç satır tutarı.</param>
/// <param name="Vat">Satır KDV'si.</param>
/// <param name="Gross">KDV dâhil satır tutarı (= Net + Vat).</param>
internal sealed record LineAmounts(decimal Net, decimal Vat, decimal Gross);

/// <summary>Fatura toplamları.</summary>
/// <param name="Net">KDV'li satırların net toplamı (Kurtaxe hariç).</param>
/// <param name="Vat">KDV toplamı.</param>
/// <param name="CityTax">Kurtaxe toplamı (KDV dışı).</param>
/// <param name="Gross">Ödenecek toplam.</param>
internal sealed record InvoiceTotals(decimal Net, decimal Vat, decimal CityTax, decimal Gross);
