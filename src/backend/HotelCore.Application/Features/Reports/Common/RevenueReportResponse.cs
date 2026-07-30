namespace HotelCore.Application.Features.Reports.Common;

/// <summary>
/// Bir gelir kaleminin üç görünümü. <b>Net mi brüt mü</b> sorusu alan adıyla yanıtlanır;
/// hiçbir yerde "tutar" diye tek başına bir alan yoktur. Her zaman <c>net + vat == gross</c>.
/// </summary>
public sealed record RevenueAmountsDto
{
    /// <summary>KDV hariç tutar (birincil ciro tanımı — Umsatzerlöse).</summary>
    public decimal Net { get; init; }

    /// <summary>KDV tutarı (otelin geliri değildir, devlete aittir).</summary>
    public decimal Vat { get; init; }

    /// <summary>KDV dâhil tutar (misafirin ödediği).</summary>
    public decimal Gross { get; init; }
}

/// <summary>
/// Konaklama gecelerine <b>dağıtılamayan</b> kesinleşmiş fatura geliri: rezervasyona bağlı
/// olmayan (elle kesilmiş) faturalar ve <c>Cancelled</c>/<c>NoShow</c> rezervasyona bağlı
/// faturalar (iptal bedeli / Ausfallentschädigung).
/// <para>
/// Dönem ataması satırın <b>Leistungsdatum</b>'una göredir. Bu blok <c>totalRevenue</c>'ya
/// <b>dâhil değildir</b> ve ADR/RevPAR'a girmez (bir oda-gecesi karşılığı yoktur). Muhasebe
/// toplamı için <c>totalRevenue.net + otherInvoicedRevenue.total.net</c> kullanılır.
/// </para>
/// </summary>
public sealed record OtherInvoicedRevenueDto
{
    /// <summary><c>RoomCharge</c> türündeki satırlar.</summary>
    public RevenueAmountsDto Room { get; init; } = new();

    /// <summary><c>Extra</c> türündeki satırlar.</summary>
    public RevenueAmountsDto Extra { get; init; } = new();

    /// <summary><c>Room + Extra</c> (Kurtaxe hariç).</summary>
    public RevenueAmountsDto Total { get; init; } = new();

    /// <summary>Bu bloktaki Kurtaxe tahsilatı — <b>gelir değildir</b>.</summary>
    public decimal CityTaxCollected { get; init; }
}

/// <summary>Kanal (<c>ReservationChannel</c>) bazında dağılım.</summary>
public sealed record RevenueByChannelDto
{
    /// <summary>Kanal enum <b>adı</b>: <c>Direct | Phone | WalkIn | BookingCom | Hrs | Expedia | Corporate</c>.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Dönemle kesişen rezervasyon sayısı (gece başına değil, konaklama başına bir kez).</summary>
    public int ReservationCount { get; init; }

    /// <summary>Bu kanala düşen satılan oda-gece.</summary>
    public int SoldRoomNights { get; init; }

    /// <summary>Bu kanala düşen konaklama geliri.</summary>
    public RevenueAmountsDto RoomRevenue { get; init; } = new();

    /// <summary>Bu kanala düşen ekstra geliri.</summary>
    public RevenueAmountsDto ExtraRevenue { get; init; } = new();

    /// <summary>Bu kanalda tahsil edilen Kurtaxe — gelir değildir.</summary>
    public decimal CityTaxCollected { get; init; }

    /// <summary>Kanalın ADR'si (net oda geliri / kanalın oda-gecesi).</summary>
    public decimal AdrNet { get; init; }

    /// <summary>Kanalın toplam <b>net oda geliri</b> içindeki payı, yüzde.</summary>
    public decimal RoomRevenueShare { get; init; }
}

/// <summary>Ciro raporunun otel kırılımı (konsolide modda anlamlı, tek otelde tek satır).</summary>
public sealed record RevenueByHotelDto
{
    public Guid HotelId { get; init; }

    public string HotelName { get; init; } = string.Empty;

    /// <summary>Otelin para birimi — konsolide modda toplamların karşılaştırılabilirliği için.</summary>
    public string Currency { get; init; } = string.Empty;

    public int SoldRoomNights { get; init; }

    public int AvailableRoomNights { get; init; }

    public decimal OccupancyRate { get; init; }

    public RevenueAmountsDto RoomRevenue { get; init; } = new();

    public RevenueAmountsDto ExtraRevenue { get; init; } = new();

    public RevenueAmountsDto TotalRevenue { get; init; } = new();

    public decimal CityTaxCollected { get; init; }

    public decimal AdrNet { get; init; }

    public decimal RevParNet { get; init; }
}

/// <summary>Ciro raporunun bir günü (grafik ekseni).</summary>
public sealed record RevenueDayDto
{
    public DateOnly Date { get; init; }

    public int SoldRoomNights { get; init; }

    public int AvailableRoomNights { get; init; }

    public decimal OccupancyRate { get; init; }

    /// <summary>O geceye <b>eşit dağıtım</b> ile düşen konaklama geliri.</summary>
    public RevenueAmountsDto RoomRevenue { get; init; } = new();

    /// <summary>O geceye düşen ekstra geliri.</summary>
    public RevenueAmountsDto ExtraRevenue { get; init; } = new();

    /// <summary>O gece tahsil edilen Kurtaxe — gelir değildir.</summary>
    public decimal CityTaxCollected { get; init; }

    public decimal AdrNet { get; init; }

    public decimal RevParNet { get; init; }
}

/// <summary>
/// <c>GET /api/v1/reports/revenue?from=&amp;to=</c> yanıtı.
///
/// <para><b>Ciro kaynağı: kesinleşmiş faturalar</b> (muhasebe gerçeği). <c>Draft</c> sayılmaz;
/// kesinleştikten sonra iptal edilen fatura ile Stornorechnung'u birlikte sayılır ve birbirini
/// sıfırlar. <b>Henüz faturalanmamış konaklamalar bu ciroya girmez</b> — operasyonel fark
/// <see cref="UnbilledRoomRevenueGross"/> alanında ayrıca gösterilir.</para>
///
/// <para><b>Kurtaxe gelir değildir</b> (belediye adına tahsil edilir): <see cref="CityTaxCollected"/>
/// ayrı alandır, <see cref="TotalRevenue"/>'ya ve ADR'ye girmez.</para>
///
/// <para>Tanımların tamamı <c>docs/api-contracts-reports.md</c> ve
/// <see cref="ReportDefinitions"/> / <see cref="RevenueRecognition"/> içindedir.</para>
/// </summary>
public sealed record RevenueReportResponse
{
    public DateOnly From { get; init; }

    /// <summary>Son gün — <b>dâhil</b>.</summary>
    public DateOnly To { get; init; }

    public int DayCount { get; init; }

    public ReportScopeDto Scope { get; init; } = new();

    /// <summary>Satılan oda-gece (ADR'nin paydası) — doluluk raporuyla <b>aynı</b> tanım.</summary>
    public int SoldRoomNights { get; init; }

    /// <summary>Satılabilir kapasite (RevPAR'ın paydası) — doluluk raporuyla <b>aynı</b> tanım.</summary>
    public int AvailableRoomNights { get; init; }

    /// <summary>Servis dışı kapasite — tüketici kendi doluluk tanımını kurabilsin diye döner.</summary>
    public int OutOfOrderRoomNights { get; init; }

    /// <summary>Fiziksel kapasite = oda sayısı × gün sayısı.</summary>
    public int PhysicalRoomNights { get; init; }

    public decimal OccupancyRate { get; init; }

    /// <summary>Konaklama geliri (<c>InvoiceLineType.RoomCharge</c>) — ADR/RevPAR'ın payı.</summary>
    public RevenueAmountsDto RoomRevenue { get; init; } = new();

    /// <summary>Ekstra geliri (<c>InvoiceLineType.Extra</c>: kahvaltı, minibar, otopark…).</summary>
    public RevenueAmountsDto ExtraRevenue { get; init; } = new();

    /// <summary>
    /// <c>roomRevenue + extraRevenue</c>. <b>Kurtaxe dâhil değildir</b> ve dağıtılamayan
    /// gelir (<see cref="OtherInvoicedRevenue"/>) da dâhil değildir.
    /// </summary>
    public RevenueAmountsDto TotalRevenue { get; init; } = new();

    /// <summary>
    /// Tahsil edilen Kurtaxe (<c>InvoiceLineType.CityTax</c>) — <b>gelir değildir</b>, belediye
    /// adına tahsil edilir ve KDV matrahına girmez.
    /// </summary>
    public decimal CityTaxCollected { get; init; }

    /// <summary>ADR (net) = net konaklama geliri / satılan oda-gece.</summary>
    public decimal AdrNet { get; init; }

    /// <summary>ADR (brüt) = brüt konaklama geliri / satılan oda-gece.</summary>
    public decimal AdrGross { get; init; }

    /// <summary>RevPAR (net) = net konaklama geliri / <b>müsait</b> oda-gece = ADR × doluluk.</summary>
    public decimal RevParNet { get; init; }

    /// <summary>RevPAR (brüt) = brüt konaklama geliri / müsait oda-gece.</summary>
    public decimal RevParGross { get; init; }

    /// <summary>
    /// <b>Operasyonel karşılaştırma alanı:</b> dönemle kesişen ama <b>henüz kesinleşmiş faturası
    /// olmayan</b> konaklamaların <c>Reservation.TotalAmount</c> tutarından gecelere düşen pay
    /// (brüt). Ciro <b>değildir</b> ve hiçbir toplama dâhil edilmez; "faturalanmayı bekleyen"
    /// büyüklüğü gösterir.
    /// </summary>
    public decimal UnbilledRoomRevenueGross { get; init; }

    /// <summary>Konaklama gecelerine dağıtılamayan kesinleşmiş fatura geliri (ayrı blok).</summary>
    public OtherInvoicedRevenueDto OtherInvoicedRevenue { get; init; } = new();

    /// <summary>Kanal dağılımı — net oda gelirine göre azalan sırada.</summary>
    public IReadOnlyList<RevenueByChannelDto> ByChannel { get; init; } = [];

    /// <summary>Otel kırılımı — tek otel modunda tek eleman.</summary>
    public IReadOnlyList<RevenueByHotelDto> ByHotel { get; init; } = [];

    /// <summary>
    /// Gün bazında seri. <b>Not:</b> günlük değerler tek tek yuvarlandığı için serinin toplamı
    /// üst seviye toplamdan birkaç kuruş sapabilir; üst seviye toplamlar yuvarlanmamış ara
    /// değerlerden hesaplanır ve <b>esas</b> alınmalıdır.
    /// </summary>
    public IReadOnlyList<RevenueDayDto> Daily { get; init; } = [];
}
