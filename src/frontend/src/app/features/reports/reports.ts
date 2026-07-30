import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { RESERVATION_CHANNEL_LABEL_KEYS } from '../../core/models/reservation.model';
import { REPORT_MAX_DAYS, REPORT_SCOPE_MODE_LABEL_KEYS } from '../../core/models/report.model';
import { isIsoDate } from '../../shared/forms/date-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { NumberPipe } from '../../shared/pipes/number.pipe';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import {
  REPORT_QUICK_RANGES,
  REPORT_QUICK_RANGE_LABEL_KEYS,
  clampReportRange,
  isQuickRangeActive,
  parseReportRange,
  quickReportRange,
  rangeDayCount,
  reportRangeToParams,
  type ReportQuickRange,
  type ReportRange,
} from './reports-query';
import { ReportsStore } from './reports.store';

/**
 * Raporlama ekrani (`GET /reports/occupancy` + `GET /reports/revenue`).
 *
 * **Bu ekranin isi rakam gostermek degil, rakamin ne oldugunu dogru
 * anlatmaktir.** Sozlesmedeki ayrimlar arayuzde gorunur kilinir:
 *
 * - Net ve brut ADR/RevPAR **ayri** alanlardir ve etiketlerinde hangisi oldugu
 *   yazar; hicbir yerde "ADR" tek basina gosterilmez.
 * - **Kurtaxe gelir degildir**: ciro toplaminin disinda, "belediye adina tahsil
 *   edilen" aciklamasiyla ayri durur.
 * - **Faturalanmamis konaklamalar** ve **dagitilamayan fatura geliri** hicbir
 *   toplama girmez; ikisi de ayri bloklardadir.
 * - **Kapasite uclusu** birlikte gosterilir ki dolulugun paydasi belli olsun;
 *   doluluk %100'u asarsa gizlenmez, kucuk bir aciklama cikar.
 * - Konsolide modda `scope` ve `hasMixedCurrencies` acikca yazilir; karisik para
 *   biriminde tutarlar **sembolsuz** gosterilir (bkz. `ReportsStore.currency`).
 *
 * **366 gun siniri istemcide** uygulanir (`parseReportRange` + store'daki son
 * savunma hatti): kullanici elle uzun bir adres yazsa bile sunucudan 400 almaz.
 *
 * Iki uc **bagimsizdir**: biri hata verdiginde digeri render edilmeye devam
 * eder ve yalnizca ilgili bolum hata blogu gosterir.
 */
@Component({
  selector: 'hc-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslatePipe,
    LocalizedDatePipe,
    MoneyPipe,
    NumberPipe,
    PageHeader,
    EmptyState,
    Spinner,
    Button,
  ],
  templateUrl: './reports.html',
})
export class ReportsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(ReportsStore);

  protected readonly maxDays = REPORT_MAX_DAYS;
  protected readonly quickRanges = REPORT_QUICK_RANGES;
  protected readonly quickRangeLabelKeys = REPORT_QUICK_RANGE_LABEL_KEYS;
  protected readonly scopeModeLabelKeys = REPORT_SCOPE_MODE_LABEL_KEYS;

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi; 366 gunu asan donem kirpilarak okunur. */
  protected readonly range = computed(() => parseReportRange(this.queryParams()));
  protected readonly dayCount = computed(() => rangeDayCount(this.range()));

  constructor() {
    // Donem degistikce iki uc da paralel yenilenir (ilk yukleme dahil).
    effect(() => {
      const range = this.range();
      void this.store.load({ from: range.from, to: range.to });
    });
  }

  protected isQuickActive(kind: ReportQuickRange): boolean {
    return isQuickRangeActive(this.range(), kind);
  }

  protected applyQuickRange(kind: ReportQuickRange): void {
    void this.navigate(quickReportRange(kind));
  }

  /** Bicimsiz/bos tarih yok sayilir (kullanici alani temizlemis olabilir). */
  protected changeFrom(value: string): void {
    if (isIsoDate(value)) {
      void this.navigate(clampReportRange(value, this.range().to));
    }
  }

  protected changeTo(value: string): void {
    if (isIsoDate(value)) {
      void this.navigate(clampReportRange(this.range().from, value));
    }
  }

  protected retryOccupancy(): void {
    void this.store.reloadOccupancy();
  }

  protected retryRevenue(): void {
    void this.store.reloadRevenue();
  }

  /** Bilinmeyen kanal adinda ham deger gosterilir (uydurma etiket uretilmez). */
  protected channelLabelKey(channel: string): string | null {
    return (RESERVATION_CHANNEL_LABEL_KEYS as Readonly<Record<string, string>>)[channel] ?? null;
  }

  private navigate(range: ReportRange): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: reportRangeToParams(range),
      queryParamsHandling: 'merge',
    });
  }
}
