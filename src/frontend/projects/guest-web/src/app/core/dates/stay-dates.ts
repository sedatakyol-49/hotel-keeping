/**
 * Konaklama tarihi yardimcilari — **saf** fonksiyonlar (DI/DOM yok, SSR'da da
 * calisir).
 *
 * KURAL: konaklama gunleri her zaman `yyyy-MM-dd` dizesidir ve `Date`'e
 * cevrilmez. `new Date('2026-08-10')` UTC gece yarisi demektir; negatif
 * offsetli bir tarayicida gun BIR GERI kayar ve misafir bir gece eksik
 * rezervasyon yapar. Aritmetik gerekince yerel ogle vakti (12:00) uzerinden
 * yapilir; boylece yaz saati gecisleri de gunu kaydiramaz.
 */

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

export function isIsoDate(value: string): boolean {
  if (!ISO_DATE.test(value)) {
    return false;
  }
  const [year, month, day] = value.split('-').map(Number);
  const date = new Date(year, month - 1, day, 12);
  return (
    date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
  );
}

export function toIsoDate(date: Date): string {
  const year = String(date.getFullYear()).padStart(4, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** Tarayicinin/sunucunun yerel bugunu. Otelin yerel gunu sunucuda dogrulanir. */
export function todayIso(now: Date = new Date()): string {
  return toIsoDate(now);
}

export function addDays(isoDate: string, days: number): string {
  const [year, month, day] = isoDate.split('-').map(Number);
  const date = new Date(year, month - 1, day, 12);
  date.setDate(date.getDate() + days);
  return toIsoDate(date);
}

/** `[checkIn, checkOut)` yari acik araligindaki gece sayisi. */
export function nightsBetween(checkIn: string, checkOut: string): number {
  if (!isIsoDate(checkIn) || !isIsoDate(checkOut)) {
    return 0;
  }
  const [y1, m1, d1] = checkIn.split('-').map(Number);
  const [y2, m2, d2] = checkOut.split('-').map(Number);
  const from = Date.UTC(y1, m1 - 1, d1);
  const to = Date.UTC(y2, m2 - 1, d2);
  return Math.round((to - from) / 86_400_000);
}

/** Varsayilan arama araligi: yarindan itibaren iki gece. */
export function defaultStay(now: Date = new Date()): { checkIn: string; checkOut: string } {
  const checkIn = addDays(todayIso(now), 1);
  return { checkIn, checkOut: addDays(checkIn, 2) };
}
