/**
 * Raporlama tipleri (docs/api-contracts-reports.md).
 *
 * Bu modulde asil zorluk kod degil **tanimlarin tutarliligidir**. Tip
 * tanimlarindaki yorumlar sozlesmedeki ayrimlari birebir tasir; arayuz de ayni
 * ayrimlari gorunur kilmak zorundadir:
 *
 * - **Net ve brut ADR/RevPAR ayri alanlardir** — tek bir sayi iki anlamda
 *   kullanilmaz.
 * - **Kurtaxe gelir degildir** (`cityTaxCollected`): ne `totalRevenue`'ya ne
 *   ADR'ye girer, otel yalnizca belediye adina tahsil eder.
 * - **`unbilledRoomRevenueGross` hicbir toplama girmez** — kesinlesmis faturasi
 *   olmayan konaklamalarin operasyonel karsiligidir, ciro degildir.
 * - **`otherInvoicedRevenue` `totalRevenue`'ya dahil DEGILDIR** — bir konaklama
 *   gecesine dagitilamayan (elle kesilen / iptal-no-show) fatura geliridir.
 * - **Kapasite uclusu** (`physicalRoomNights`, `outOfOrderRoomNights`,
 *   `availableRoomNights`) her zaman birlikte gosterilir ki dolulugun hangi
 *   paydaya gore hesaplandigi belli olsun.
 */

/** Para blogu — sunucu `net + vat == gross` esitligini garanti eder. */
export interface MoneyBlock {
  readonly net: number;
  readonly vat: number;
  readonly gross: number;
}

/** Rapor kapsami: tek otel mi, konsolide mi. */
export const REPORT_SCOPE_MODES = ['Hotel', 'Consolidated'] as const;

export type ReportScopeMode = (typeof REPORT_SCOPE_MODES)[number];

export const REPORT_SCOPE_MODE_LABEL_KEYS: Readonly<Record<ReportScopeMode, string>> = {
  Hotel: 'reports.scope.mode.hotel',
  Consolidated: 'reports.scope.mode.consolidated',
};

/**
 * `scope` — sayilarin **hangi kapsamda** hesaplandigi.
 *
 * `hasMixedCurrencies: true` ise ust seviye para toplamlari farkli birimlerin
 * aritmetik toplamidir ve **kullanilmamalidir**; sayi gizlenmez, **etiketlenir**
 * ve para birimi sembolu olmadan gosterilir (bkz. `ReportsStore.currency`).
 */
export interface ReportScope {
  readonly mode: ReportScopeMode;
  /** Konsolide modda `null`. */
  readonly hotelId?: string | null;
  readonly hotelCount: number;
  /** Ortak para birimi; karisiksa `null`. */
  readonly currency?: string | null;
  readonly hasMixedCurrencies: boolean;
}

/**
 * `GET /reports/occupancy` ve `GET /reports/revenue` sorgu araligi.
 *
 * **Kapali aralik** `[from, to]` — `to` DAHILDIR ve `to == from` tek gunluk
 * gecerli bir rapordur. Rezervasyon modulunun yari acik `[checkIn, checkOut)`
 * araligindan bilincli farktir: rapor bir **gun kumesi** uzerinde konusur.
 */
export interface ReportRangeQuery {
  readonly from: string;
  /** **Dahil.** */
  readonly to: string;
}

/** Sunucu tavani: `to - from + 1 <= 366`, asilirsa 400 (sessizce kirpilmaz). */
export const REPORT_MAX_DAYS = 366;

/** Kapali araliktaki gun sayisi (`to - from + 1`); ters/gecersizde `null`. */
export function reportDayCount(from: string, to: string): number | null {
  const start = Date.parse(`${from}T00:00:00Z`);
  const end = Date.parse(`${to}T00:00:00Z`);
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return null;
  }
  const days = Math.round((end - start) / 86_400_000) + 1;
  return days >= 1 ? days : null;
}

// --- Doluluk raporu --------------------------------------------------------

/** Gun basina tek satir — grafik ekseni. Para alani **yoktur**. */
export interface OccupancyReportDaily {
  readonly date: string;
  readonly soldRoomNights: number;
  readonly availableRoomNights: number;
  /** Yuzde; **%100'u asabilir** (servis disi bayragi tarihsizdir). */
  readonly occupancyRate: number;
}

/** Doluluk raporunun otel kirilimi (tek otel modunda tek eleman). */
export interface OccupancyReportByHotel {
  readonly hotelId: string;
  readonly hotelName: string;
  readonly roomCount: number;
  readonly outOfOrderRoomCount: number;
  readonly physicalRoomNights: number;
  readonly outOfOrderRoomNights: number;
  readonly availableRoomNights: number;
  readonly soldRoomNights: number;
  readonly occupancyRate: number;
}

/** `GET /reports/occupancy?from=&to=` — `Reports.View`. */
export interface OccupancyReportResponse {
  readonly from: string;
  /** **Dahil.** */
  readonly to: string;
  readonly dayCount: number;
  readonly scope: ReportScope;
  readonly roomCount: number;
  readonly outOfOrderRoomCount: number;
  /** Oda sayisi × gun sayisi (servis disi **dahil**). */
  readonly physicalRoomNights: number;
  /** Servis disi oda sayisi × gun sayisi. */
  readonly outOfOrderRoomNights: number;
  /** `physical - outOfOrder` → dolulugun ve RevPAR'in **paydasi**. */
  readonly availableRoomNights: number;
  readonly soldRoomNights: number;
  /** `sold / available × 100`; **kirpilmaz**, %100'u asabilir. */
  readonly occupancyRate: number;
  readonly daily: readonly OccupancyReportDaily[];
  readonly byHotel: readonly OccupancyReportByHotel[];
}

// --- Ciro raporu -----------------------------------------------------------

/** Kanal dagilimi — net oda gelirine gore azalan sirada gelir. */
export interface RevenueReportByChannel {
  /** `ReservationChannel` enum **adi**. */
  readonly channel: string;
  /** Donemle **kesisen** rezervasyon sayisi (gece basina degil). */
  readonly reservationCount: number;
  readonly soldRoomNights: number;
  readonly roomRevenue: MoneyBlock;
  readonly extraRevenue: MoneyBlock;
  /** Kurtaxe — kanal cirosuna **dahil degildir**. */
  readonly cityTaxCollected: number;
  readonly adrNet: number;
  /** Toplam **net oda geliri** icindeki pay, yuzde. */
  readonly roomRevenueShare: number;
}

/** Ciro raporunun otel kirilimi; karisik para biriminde **esas alinir**. */
export interface RevenueReportByHotel {
  readonly hotelId: string;
  readonly hotelName: string;
  readonly currency: string;
  readonly soldRoomNights: number;
  readonly availableRoomNights: number;
  readonly occupancyRate: number;
  readonly roomRevenue: MoneyBlock;
  readonly extraRevenue: MoneyBlock;
  readonly totalRevenue: MoneyBlock;
  readonly cityTaxCollected: number;
  readonly adrNet: number;
  readonly revParNet: number;
}

/** Gun basina ciro satiri. Yuvarlanmis gunluk toplamlar ust toplamdan sapabilir. */
export interface RevenueReportDaily {
  readonly date: string;
  readonly soldRoomNights: number;
  readonly availableRoomNights: number;
  readonly occupancyRate: number;
  readonly roomRevenue: MoneyBlock;
  readonly extraRevenue: MoneyBlock;
  readonly cityTaxCollected: number;
  readonly adrNet: number;
  readonly revParNet: number;
}

/**
 * Konaklama gecelerine **dagitilamayan** kesinlesmis fatura geliri
 * (elle kesilen faturalar + iptal/no-show rezervasyona bagli faturalar),
 * satirin Leistungsdatum'una gore donemlenmis.
 *
 * `totalRevenue`'ya **dahil degildir** ve ADR/RevPAR'a girmez.
 */
export interface OtherInvoicedRevenue {
  readonly room: MoneyBlock;
  readonly extra: MoneyBlock;
  readonly total: MoneyBlock;
  readonly cityTaxCollected: number;
}

/** `GET /reports/revenue?from=&to=` — `Reports.View`. */
export interface RevenueReportResponse {
  readonly from: string;
  /** **Dahil.** */
  readonly to: string;
  readonly dayCount: number;
  readonly scope: ReportScope;

  // Doluluk raporuyla ayni tanimlar (ADR/RevPAR paydalarinin kaynagi).
  readonly soldRoomNights: number;
  readonly availableRoomNights: number;
  readonly outOfOrderRoomNights: number;
  readonly physicalRoomNights: number;
  readonly occupancyRate: number;

  readonly roomRevenue: MoneyBlock;
  readonly extraRevenue: MoneyBlock;
  /** `room + extra` — **Kurtaxe haric**. */
  readonly totalRevenue: MoneyBlock;
  /** Kurtaxe — **gelir degildir**, ciro toplamina ve ADR'ye girmez. */
  readonly cityTaxCollected: number;

  /** Oda geliri / `soldRoomNights` — ekstra ve Kurtaxe girmez. */
  readonly adrNet: number;
  readonly adrGross: number;
  /** Oda geliri / `availableRoomNights` (= ADR × doluluk). */
  readonly revParNet: number;
  readonly revParGross: number;

  /** Kesinlesmis faturasi **olmayan** konaklamalar. Hicbir toplama girmez. */
  readonly unbilledRoomRevenueGross: number;
  readonly otherInvoicedRevenue: OtherInvoicedRevenue;

  readonly byChannel: readonly RevenueReportByChannel[];
  readonly byHotel: readonly RevenueReportByHotel[];
  readonly daily: readonly RevenueReportDaily[];
}
