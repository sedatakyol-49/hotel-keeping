import type { ParamMap, Params } from '@angular/router';

import { currentIsoWeekLabel, isoWeekLabel, parseIsoWeekLabel } from './iso-week';

/**
 * `?week=YYYY-Www` <-> secili hafta.
 *
 * URL tek dogruluk kaynagidir: haftalar arasinda gezinmek adres cubugunu
 * gunceller, sayfa yenilendiginde ayni hafta acilir ve baglanti paylasilabilir.
 * Gecersiz etiket sessizce **bu haftaya** duser (elle duzenlenmis bir adres
 * ekrani kirmaz; sunucu da gecersiz etikete 400 dondururdu).
 *
 * Bulundugumuz hafta adres cubuguna **yazilmaz** — mevcut ekranlardaki
 * "varsayilani URL'e yazma" kuralinin aynisi; boylece `/shifts` her zaman
 * "bu hafta" anlamini korur.
 */
export function parseShiftWeekParam(params: ParamMap, now: Date = new Date()): string {
  const parsed = parseIsoWeekLabel(params.get('week'));
  return parsed === null ? currentIsoWeekLabel(now) : isoWeekLabel(parsed);
}

export function shiftWeekToParams(week: string, now: Date = new Date()): Params {
  return week === currentIsoWeekLabel(now) ? { week: null } : { week };
}

/** Secili hafta bulundugumuz hafta mi ("bu hafta" dugmesini kilitlemek icin). */
export function isCurrentIsoWeek(week: string, now: Date = new Date()): boolean {
  return week === currentIsoWeekLabel(now);
}
