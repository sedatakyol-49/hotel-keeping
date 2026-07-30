import type { ParamMap, Params } from '@angular/router';

import {
  OCCUPANCY_MAX_DAYS,
  addDays,
  nightsBetween,
  todayIso,
} from '../../core/models/availability.model';
import { isIsoDate } from '../../shared/forms/date-validators';

/**
 * Doluluk plani tarih araligi — **URL tek dogruluk kaynagi**
 * (`?from=2026-08-09&to=2026-08-23`).
 *
 * Sunucu `/occupancy` icin araligi **en fazla 92 gun** kabul eder ve asilirsa
 * 400 doner. Istemci bu siniri **istek gondermeden once** uygular: kullanici
 * elle duzenlenmis bir adresle (ornek: yillik matris) ekrani kiramaz ve bos yere
 * 400 alinmaz. Kirpma sessizce yapilmaz — `clamped` bayragi ile ekranda
 * aciklanir.
 */

/** Varsayilan pencere: bugunden itibaren iki hafta. */
export const DEFAULT_OCCUPANCY_NIGHTS = 14;

/** Hazir pencere genislikleri (gece). 92 sunucu tavani oldugu icin en fazla 31. */
export const OCCUPANCY_RANGE_OPTIONS = [7, 14, 31] as const;

export interface OccupancyRange {
  readonly from: string;
  /** **Dahil degil** (yari acik aralik). */
  readonly to: string;
  /** Sunucu tavani (92 gun) asildigi icin `to` kirpildi mi. */
  readonly clamped: boolean;
}

/** Aralikta kac gece var (kolon sayisi). */
export function rangeNights(range: Pick<OccupancyRange, 'from' | 'to'>): number {
  return nightsBetween(range.from, range.to) ?? 0;
}

/**
 * Araligi gecerli hale getirir:
 * en az **1 gece**, en fazla **`OCCUPANCY_MAX_DAYS`** gece.
 */
export function clampOccupancyRange(from: string, to: string): OccupancyRange {
  const nights = nightsBetween(from, to);

  if (nights === null || nights < 1) {
    // Ters veya sifir aralik: sunucu `to > from` ister.
    return { from, to: addDays(from, 1), clamped: false };
  }
  if (nights > OCCUPANCY_MAX_DAYS) {
    return { from, to: addDays(from, OCCUPANCY_MAX_DAYS), clamped: true };
  }
  return { from, to, clamped: false };
}

/** Bugunden baslayan varsayilan pencere. */
export function defaultOccupancyRange(now: Date = new Date()): OccupancyRange {
  const from = todayIso(now);
  return { from, to: addDays(from, DEFAULT_OCCUPANCY_NIGHTS), clamped: false };
}

/**
 * URL sorgu parametreleri -> `OccupancyRange`.
 * Gecersiz/eksik degerler varsayilana duser; 92 gunu asan aralik kirpilir.
 */
export function parseOccupancyRange(params: ParamMap, now: Date = new Date()): OccupancyRange {
  const fallback = defaultOccupancyRange(now);
  const rawFrom = params.get('from')?.trim();
  const rawTo = params.get('to')?.trim();

  const from = rawFrom && isIsoDate(rawFrom) ? rawFrom : fallback.from;
  const to =
    rawTo && isIsoDate(rawTo) ? rawTo : addDays(from, DEFAULT_OCCUPANCY_NIGHTS);

  return clampOccupancyRange(from, to);
}

/**
 * `OccupancyRange` -> URL sorgu parametreleri.
 * Varsayilan pencere adres cubugunu kirletmesin diye yazilmaz; `from`
 * varsayilani "bugun" oldugu icin tarih degistiginde baglanti bayatlamaz.
 */
export function occupancyRangeToParams(range: OccupancyRange, now: Date = new Date()): Params {
  const fallback = defaultOccupancyRange(now);
  const isDefault = range.from === fallback.from && range.to === fallback.to;

  if (isDefault) {
    return { from: null, to: null };
  }
  return { from: range.from, to: range.to };
}

/** Pencereyi kendi genisligi kadar ileri/geri kaydirir (hafta/ay gezinmesi). */
export function shiftOccupancyRange(range: OccupancyRange, direction: 1 | -1): OccupancyRange {
  const nights = rangeNights(range);
  const step = nights > 0 ? nights : DEFAULT_OCCUPANCY_NIGHTS;
  const from = addDays(range.from, direction * step);
  return clampOccupancyRange(from, addDays(from, step));
}

/** Baslangici koruyup pencere genisligini degistirir. */
export function resizeOccupancyRange(range: OccupancyRange, nights: number): OccupancyRange {
  return clampOccupancyRange(range.from, addDays(range.from, nights));
}

/** Genisligi koruyup pencereyi bugune tasir. */
export function moveOccupancyRangeToToday(
  range: OccupancyRange,
  now: Date = new Date(),
): OccupancyRange {
  const nights = rangeNights(range);
  const from = todayIso(now);
  return clampOccupancyRange(from, addDays(from, nights > 0 ? nights : DEFAULT_OCCUPANCY_NIGHTS));
}

export function isOccupancyRangeAtToday(
  range: Pick<OccupancyRange, 'from'>,
  now: Date = new Date(),
): boolean {
  return range.from === todayIso(now);
}
