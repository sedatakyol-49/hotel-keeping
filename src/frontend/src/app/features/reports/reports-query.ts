import type { ParamMap, Params } from '@angular/router';

import { addDays, todayIso } from '../../core/models/availability.model';
import { REPORT_MAX_DAYS, reportDayCount } from '../../core/models/report.model';
import { isIsoDate } from '../../shared/forms/date-validators';

/**
 * Rapor donemi — **URL tek dogruluk kaynagi** (`?from=2026-07-01&to=2026-07-30`).
 *
 * Donem **kapali** araliktir: `to` DAHILDIR ve `to == from` tek gunluk gecerli
 * bir rapordur (rezervasyon modulunun yari acik araligindan bilincli fark —
 * rapor bir gun kumesi uzerinde konusur).
 *
 * Sunucu `to - from + 1 <= 366` ister ve asilirsa **400** doner. Istemci bu
 * siniri **istek gondermeden once** uygular: kullanici elle duzenlenmis bir
 * adresle (ornek: bes yillik donem) sunucudan 400 almaz. Kirpma sessizce
 * yapilmaz — `clamped` bayragi ekranda aciklanir.
 */

/** Varsayilan donem: bugun dahil **son 30 gun**. */
export const DEFAULT_REPORT_DAYS = 30;

export interface ReportRange {
  readonly from: string;
  /** **Dahil** (kapali aralik). */
  readonly to: string;
  /** 366 gun tavani asildigi icin `to` kirpildi mi. */
  readonly clamped: boolean;
}

/** Hazir donemler. `thisMonth` bilincli olarak **ay basindan bugune**dir. */
export const REPORT_QUICK_RANGES = ['last7', 'last30', 'thisMonth', 'lastMonth'] as const;

export type ReportQuickRange = (typeof REPORT_QUICK_RANGES)[number];

export const REPORT_QUICK_RANGE_LABEL_KEYS: Readonly<Record<ReportQuickRange, string>> = {
  last7: 'reports.range.quick.last7',
  last30: 'reports.range.quick.last30',
  thisMonth: 'reports.range.quick.thisMonth',
  lastMonth: 'reports.range.quick.lastMonth',
};

/** Donemdeki gun sayisi (`to - from + 1`); gecersizde 0. */
export function rangeDayCount(range: Pick<ReportRange, 'from' | 'to'>): number {
  return reportDayCount(range.from, range.to) ?? 0;
}

/**
 * Araligi gecerli hale getirir: en az **1 gun**, en fazla **366 gun**.
 * Ters aralik tek gunluk rapora duser (`to = from`), sunucu tavani asilirsa
 * `to` kirpilir ve bayrak birakilir.
 */
export function clampReportRange(from: string, to: string): ReportRange {
  const days = reportDayCount(from, to);

  if (days === null) {
    // Ters veya bicimsiz aralik: tek gunluk rapor gecerlidir.
    return { from, to: from, clamped: false };
  }
  if (days > REPORT_MAX_DAYS) {
    return { from, to: addDays(from, REPORT_MAX_DAYS - 1), clamped: true };
  }
  return { from, to, clamped: false };
}

/** Bugun dahil son 30 gun. */
export function defaultReportRange(now: Date = new Date()): ReportRange {
  const to = todayIso(now);
  return { from: addDays(to, -(DEFAULT_REPORT_DAYS - 1)), to, clamped: false };
}

/** Hazir donem uretir; `thisMonth` ay basindan **bugune** kadardir. */
export function quickReportRange(kind: ReportQuickRange, now: Date = new Date()): ReportRange {
  const today = todayIso(now);

  switch (kind) {
    case 'last7':
      return { from: addDays(today, -6), to: today, clamped: false };
    case 'last30':
      return defaultReportRange(now);
    case 'thisMonth':
      // Ay sonuna kadar sorulsaydi henuz gerceklesmemis gunler RevPAR paydasini
      // sisirir ve donem yaniltici gorunurdu; bu yuzden **bugune** kadar.
      return { from: startOfMonth(today), to: today, clamped: false };
    case 'lastMonth': {
      const lastDay = addDays(startOfMonth(today), -1);
      return { from: startOfMonth(lastDay), to: lastDay, clamped: false };
    }
  }
}

/** Secili donem hangi hazir donemle birebir ayni (buton vurgusu icin). */
export function isQuickRangeActive(
  range: Pick<ReportRange, 'from' | 'to'>,
  kind: ReportQuickRange,
  now: Date = new Date(),
): boolean {
  const quick = quickReportRange(kind, now);
  return quick.from === range.from && quick.to === range.to;
}

/**
 * URL sorgu parametreleri -> `ReportRange`.
 * Gecersiz/eksik degerler varsayilana duser; 366 gunu asan aralik kirpilir.
 */
export function parseReportRange(params: ParamMap, now: Date = new Date()): ReportRange {
  const fallback = defaultReportRange(now);
  const rawFrom = params.get('from')?.trim();
  const rawTo = params.get('to')?.trim();

  const from = rawFrom && isIsoDate(rawFrom) ? rawFrom : fallback.from;
  const to = rawTo && isIsoDate(rawTo) ? rawTo : addDays(from, DEFAULT_REPORT_DAYS - 1);

  return clampReportRange(from, to);
}

/**
 * `ReportRange` -> URL sorgu parametreleri.
 *
 * Doluluk planinin aksine **her zaman** yazilir: bir rapor baglantisi
 * paylasildiginda alicinin ayni donemi gormesi gerekir; "son 30 gun" gorece bir
 * penceredir ve yarin baska bir donemi gosterirdi.
 */
export function reportRangeToParams(range: Pick<ReportRange, 'from' | 'to'>): Params {
  return { from: range.from, to: range.to };
}

/** Ayin ilk gunu (ISO). */
function startOfMonth(date: string): string {
  return `${date.slice(0, 7)}-01`;
}
