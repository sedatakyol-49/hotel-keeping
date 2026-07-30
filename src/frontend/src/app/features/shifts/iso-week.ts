/**
 * ISO 8601 hafta hesaplari (`2026-W32`).
 *
 * Neden elle: "hafta" tanimi kulture gore degisir (haftanin ilk gunu, yilin
 * ilk haftasi). Sozlesme ISO 8601 kullanir — hafta **Pazartesi** baslar ve
 * yilin ilk haftasi 4 Ocak'i iceren haftadir. Hesaplar `Intl`/locale'den
 * bagimsiz olmali, aksi halde istemcinin dil ayari plani kaydirir.
 *
 * Tum hesaplar **UTC** uzerinde yapilir: tarihler saat tasimadigi icin yerel
 * saat dilimi devreye girerse gun kayabilir.
 */

const DAY_MS = 86_400_000;

export interface IsoWeekParts {
  readonly year: number;
  readonly week: number;
}

/** `YYYY-MM-DD` (UTC gun basi) -> `Date`. */
export function isoDateToUtcDate(value: string): Date | null {
  const timestamp = Date.parse(`${value}T00:00:00Z`);
  return Number.isNaN(timestamp) ? null : new Date(timestamp);
}

/** `Date` -> `YYYY-MM-DD` (UTC parcalari). */
export function toIsoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** Yerel takvimdeki "bugun" — UTC gun basina sabitlenir. */
export function todayUtcDate(now: Date = new Date()): Date {
  return new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
}

/** Bir gunun ISO yil + hafta numarasi (Persembe kurali). */
export function isoWeekOf(date: Date): IsoWeekParts {
  const target = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  // Pazartesi = 0 ... Pazar = 6; haftanin Persembe'sine tasi (ISO yil oradan gelir).
  const dayIndex = (target.getUTCDay() + 6) % 7;
  target.setUTCDate(target.getUTCDate() - dayIndex + 3);
  const isoYear = target.getUTCFullYear();

  const firstThursday = new Date(Date.UTC(isoYear, 0, 4));
  const firstDayIndex = (firstThursday.getUTCDay() + 6) % 7;
  firstThursday.setUTCDate(firstThursday.getUTCDate() - firstDayIndex + 3);

  const week = 1 + Math.round((target.getTime() - firstThursday.getTime()) / (7 * DAY_MS));
  return { year: isoYear, week };
}

/** Bir ISO yilinda 52 mi 53 hafta var (28 Aralik her zaman son haftadadir). */
export function weeksInIsoYear(year: number): number {
  return isoWeekOf(new Date(Date.UTC(year, 11, 28))).week;
}

/** ISO yil + hafta -> haftanin Pazartesi'si (UTC). */
export function mondayOfIsoWeek(year: number, week: number): Date {
  const january4 = new Date(Date.UTC(year, 0, 4));
  const dayIndex = (january4.getUTCDay() + 6) % 7;
  const firstMonday = new Date(january4.getTime() - dayIndex * DAY_MS);
  return new Date(firstMonday.getTime() + (week - 1) * 7 * DAY_MS);
}

/** `{ year: 2026, week: 32 }` -> `"2026-W32"`. */
export function isoWeekLabel(parts: IsoWeekParts): string {
  return `${String(parts.year).padStart(4, '0')}-W${String(parts.week).padStart(2, '0')}`;
}

/**
 * `"2026-W32"` -> `{ year, week }`; bicim veya hafta numarasi gecersizse
 * (ornek: 53 haftasi olmayan bir yilda `W53`) `null`. Sunucu ayni kurali
 * uygular ve gecersiz etikete 400 doner.
 */
export function parseIsoWeekLabel(value: string | null | undefined): IsoWeekParts | null {
  if (!value) {
    return null;
  }
  const match = /^(\d{4})-W(\d{2})$/i.exec(value.trim());
  if (!match) {
    return null;
  }
  const year = Number(match[1]);
  const week = Number(match[2]);
  if (year < 1 || year > 9999 || week < 1 || week > weeksInIsoYear(year)) {
    return null;
  }
  return { year, week };
}

/** Bugunun (yerel takvim) ISO hafta etiketi. */
export function currentIsoWeekLabel(now: Date = new Date()): string {
  return isoWeekLabel(isoWeekOf(todayUtcDate(now)));
}

/**
 * Hafta etiketini `delta` hafta kaydirir. Kaydirma Pazartesi tarihi uzerinden
 * yapilir; boylece yil sonu ve 53 haftali yillar kendiliginden dogru cikar.
 */
export function shiftIsoWeekLabel(label: string, delta: number): string {
  const parts = parseIsoWeekLabel(label) ?? isoWeekOf(todayUtcDate());
  const monday = mondayOfIsoWeek(parts.year, parts.week);
  const moved = new Date(monday.getTime() + delta * 7 * DAY_MS);
  return isoWeekLabel(isoWeekOf(moved));
}

/** Haftanin Pazartesi–Pazar tarihleri (`YYYY-MM-DD`), sunucu yaniti gelmeden onizleme icin. */
export function isoWeekDates(label: string): readonly string[] {
  const parts = parseIsoWeekLabel(label);
  if (parts === null) {
    return [];
  }
  const monday = mondayOfIsoWeek(parts.year, parts.week);
  return Array.from({ length: 7 }, (_, index) =>
    toIsoDate(new Date(monday.getTime() + index * DAY_MS)),
  );
}
