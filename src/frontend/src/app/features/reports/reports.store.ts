import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ReportsApi } from '../../core/api/reports.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  REPORT_MAX_DAYS,
  reportDayCount,
  type OccupancyReportDaily,
  type OccupancyReportResponse,
  type ReportRangeQuery,
  type ReportScope,
  type RevenueReportResponse,
} from '../../core/models/report.model';

/** Gunluk seri satirinin **sunum** modeli (cubuk genisligi burada bir kez hesaplanir). */
export interface OccupancyDailyRow extends OccupancyReportDaily {
  /** Cubuk genisligi, 0–100 arasi kirpilmis yuzde (yalnizca gorsel). */
  readonly barWidth: number;
  /** Gercek oran %100'u asiyor mu — cubuk kirpilir ama **sayi kirpilmaz**. */
  readonly overCapacity: boolean;
}

/**
 * Gunluk seriyi sunuma cevirir.
 *
 * `occupancyRate` **%100'u asabilir** (servis disi bayragi tarihsizdir);
 * cubugun genisligi 100'de durur ama satirdaki sayi oldugu gibi kalir ve satir
 * `overCapacity` ile isaretlenir. Gercek gizlenmez.
 */
export function buildOccupancyDailyRows(
  daily: readonly OccupancyReportDaily[],
): readonly OccupancyDailyRow[] {
  return daily.map((day) => ({
    ...day,
    barWidth: Math.max(0, Math.min(100, day.occupancyRate)),
    overCapacity: day.occupancyRate > 100,
  }));
}

/**
 * Raporlama signal store'u — **iki bagimsiz uc**:
 * `GET /reports/occupancy` ve `GET /reports/revenue`.
 *
 * Her ucun kendi `loading`/`error` durumu vardir ve istekler paralel gider:
 * biri 500 dondugunde digeri render edilmeye devam eder (kismi rapor, bos
 * ekrandan iyidir). Ust uste gelen isteklerde **yalnizca en son** yanit yazilir
 * (uc basina `requestToken`).
 *
 * **366 gun tavani** burada da dogrulanir: asan aralikta istek hic gonderilmez
 * (sunucu 400 dondurur, kullaniciyi oraya dusurmeyiz). Normal akista
 * `parseReportRange` araligi zaten kirptigi icin bu son savunma hattidir.
 */
@Injectable({ providedIn: 'root' })
export class ReportsStore {
  private readonly api = inject(ReportsApi);

  private readonly _range = signal<ReportRangeQuery | null>(null);

  private readonly _occupancy = signal<OccupancyReportResponse | null>(null);
  private readonly _occupancyLoading = signal(false);
  private readonly _occupancyError = signal<ApiError | null>(null);

  private readonly _revenue = signal<RevenueReportResponse | null>(null);
  private readonly _revenueLoading = signal(false);
  private readonly _revenueError = signal<ApiError | null>(null);

  private occupancyToken = 0;
  private revenueToken = 0;

  readonly range = this._range.asReadonly();

  readonly occupancy = this._occupancy.asReadonly();
  readonly occupancyLoading = this._occupancyLoading.asReadonly();
  readonly occupancyError = this._occupancyError.asReadonly();

  readonly revenue = this._revenue.asReadonly();
  readonly revenueLoading = this._revenueLoading.asReadonly();
  readonly revenueError = this._revenueError.asReadonly();

  /** Gunluk seri sunum satirlari (her change detection'da yeniden hesaplanmaz). */
  readonly dailyRows = computed<readonly OccupancyDailyRow[]>(() =>
    buildOccupancyDailyRows(this._occupancy()?.daily ?? []),
  );

  /**
   * Kapsam bilgisi. Iki uc da ayni kapsami dondurur; hangisi yuklendiyse o
   * okunur ki bir uc hata verdiginde kapsam serildi kaybolmasin.
   */
  readonly scope = computed<ReportScope | null>(
    () => this._revenue()?.scope ?? this._occupancy()?.scope ?? null,
  );

  readonly isConsolidated = computed(() => this.scope()?.mode === 'Consolidated');
  readonly hasMixedCurrencies = computed(() => this.scope()?.hasMixedCurrencies === true);

  /**
   * Para birimi — **karisik para biriminde `null`**.
   *
   * `null` donmesi bilincli: ust seviye toplamlar farkli birimlerin aritmetik
   * toplamidir, bu yuzden tutarlar **para birimi sembolu olmadan** gosterilir ve
   * ekranda uyari cikar. Sayi gizlenmez, etiketlenir (sozlesme karari).
   */
  readonly currency = computed<string | null>(() => {
    const scope = this.scope();
    if (scope === null || scope.hasMixedCurrencies) {
      return null;
    }
    return scope.currency ?? null;
  });

  /** Doluluk %100'u asti mi (donem toplaminda) — aciklama gosterilir. */
  readonly occupancyOverCapacity = computed(() => (this._occupancy()?.occupancyRate ?? 0) > 100);

  /** Donemde hic kapasite ve hic satis yok — "veri yok" hali. */
  readonly occupancyEmpty = computed(() => {
    const report = this._occupancy();
    return report !== null && report.physicalRoomNights === 0 && report.soldRoomNights === 0;
  });

  /** Ciro tarafinda gosterilecek **hicbir** rakam yok. */
  readonly revenueEmpty = computed(() => {
    const report = this._revenue();
    return (
      report !== null &&
      report.totalRevenue.gross === 0 &&
      report.cityTaxCollected === 0 &&
      report.unbilledRoomRevenueGross === 0 &&
      report.otherInvoicedRevenue.total.gross === 0
    );
  });

  /**
   * Sozlesmenin acikca izin verdigi **tek** turetilmis toplam:
   * `totalRevenue.net + otherInvoicedRevenue.total.net`.
   *
   * Kurtaxe ve `unbilledRoomRevenueGross` bu toplama **girmez** — biri gelir
   * degildir, digeri kesinlesmis bir belgeye dayanmaz.
   */
  readonly accountingTotalNet = computed<number | null>(() => {
    const report = this._revenue();
    return report === null ? null : report.totalRevenue.net + report.otherInvoicedRevenue.total.net;
  });

  /** Her iki uc da paralel yuklenir; biri patlarsa digeri etkilenmez. */
  async load(range: ReportRangeQuery): Promise<void> {
    this._range.set(range);
    await Promise.all([this.loadOccupancy(range), this.loadRevenue(range)]);
  }

  async reloadOccupancy(): Promise<void> {
    const range = this._range();
    if (range) {
      await this.loadOccupancy(range);
    }
  }

  async reloadRevenue(): Promise<void> {
    const range = this._range();
    if (range) {
      await this.loadRevenue(range);
    }
  }

  private async loadOccupancy(range: ReportRangeQuery): Promise<void> {
    const token = ++this.occupancyToken;
    if (!isRequestableRange(range)) {
      this._occupancy.set(null);
      this._occupancyError.set(RANGE_TOO_LONG);
      this._occupancyLoading.set(false);
      return;
    }

    this._occupancyLoading.set(true);
    this._occupancyError.set(null);
    try {
      const response = await firstValueFrom(this.api.occupancy(range));
      if (token !== this.occupancyToken) {
        return;
      }
      this._occupancy.set(response);
    } catch (error: unknown) {
      if (token !== this.occupancyToken) {
        return;
      }
      this._occupancy.set(null);
      this._occupancyError.set(toApiError(error));
    } finally {
      if (token === this.occupancyToken) {
        this._occupancyLoading.set(false);
      }
    }
  }

  private async loadRevenue(range: ReportRangeQuery): Promise<void> {
    const token = ++this.revenueToken;
    if (!isRequestableRange(range)) {
      this._revenue.set(null);
      this._revenueError.set(RANGE_TOO_LONG);
      this._revenueLoading.set(false);
      return;
    }

    this._revenueLoading.set(true);
    this._revenueError.set(null);
    try {
      const response = await firstValueFrom(this.api.revenue(range));
      if (token !== this.revenueToken) {
        return;
      }
      this._revenue.set(response);
    } catch (error: unknown) {
      if (token !== this.revenueToken) {
        return;
      }
      this._revenue.set(null);
      this._revenueError.set(toApiError(error));
    } finally {
      if (token === this.revenueToken) {
        this._revenueLoading.set(false);
      }
    }
  }
}

/**
 * Istemci tarafi aralik dogrulamasi: 366 gunu asan veya bicimsiz aralikta
 * istek **hic gonderilmez** (sunucu 400 dondururdu).
 */
function isRequestableRange(range: ReportRangeQuery): boolean {
  const days = reportDayCount(range.from, range.to);
  return days !== null && days <= REPORT_MAX_DAYS;
}

/** Istemcide uretilen 400 — sunucuya gidilmedigi icin `traceId` yoktur. */
const RANGE_TOO_LONG: ApiError = {
  status: 400,
  messageKey: 'reports.range.tooLong',
  fieldErrors: { To: ['reports.range.tooLong'] },
};
