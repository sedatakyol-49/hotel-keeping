/**
 * Zeiterfassung (TimeEntry) modulu tipleri
 * (api-contracts.md → "Zeiterfassung (TimeEntry)").
 *
 * `clockIn` / `clockOut` **zaman damgasidir** (offset'li ISO):
 * `"2026-07-29T06:00:00+00:00"`. Ekranda yerel saat gosterilir; forma
 * `datetime-local` ile girilen deger tekrar ISO'ya cevrilerek gonderilir.
 */

/** `TimeEntryResponse` — `workedMinutes` acik kayitta `null`. */
export interface TimeEntryResponse {
  readonly id: string;
  readonly employeeId: string;
  readonly employeeName: string;
  readonly clockIn: string;
  readonly clockOut?: string | null;
  readonly breakMinutes: number;
  /** `(clockOut - clockIn) - mola`; acik kayitta `null`. */
  readonly workedMinutes?: number | null;
  /** Backend enum adi (`Manual`, ...) — istemci yalnizca gosterir. */
  readonly source?: string | null;
  readonly note?: string | null;
  /** `clockOut === null` — sunucu hesaplar. */
  readonly isOpen: boolean;
}

/** `GET /time-entries` filtreleri: `?page&pageSize&employeeId=&from=&to=`. */
export interface TimeEntryListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly employeeId?: string | null;
  /** ISO tarih (`YYYY-MM-DD`) — sunucu gun bazinda suzer. */
  readonly from?: string | null;
  readonly to?: string | null;
}

/** `POST /time-entries/clock-in` — `clockIn` bos ise sunucu saati kullanilir. */
export interface ClockInRequest {
  readonly employeeId: string;
  readonly clockIn?: string | null;
  readonly note?: string | null;
}

/** `POST /time-entries/clock-out` — `clockOut` bos ise sunucu saati kullanilir. */
export interface ClockOutRequest {
  readonly employeeId: string;
  readonly clockOut?: string | null;
  readonly breakMinutes?: number | null;
  readonly note?: string | null;
}

/** `PUT /time-entries/{id}` — manuel duzeltme. */
export interface UpdateTimeEntryRequest {
  readonly clockIn: string;
  readonly clockOut?: string | null;
  readonly breakMinutes: number;
  readonly note?: string | null;
}

/**
 * Sozlesmedeki sinirlar: `breakMinutes` 0–1440 **ve** brut calisma suresini
 * asamaz (sunucu 400 doner ve mesajinda mevcut sureyi soyler; o mesaj
 * `errors.BreakMinutes` uzerinden ilgili alana baglanir).
 */
export const TIME_ENTRY_LIMITS = {
  breakMinutesMin: 0,
  breakMinutesMax: 1440,
  noteMaxLength: 500,
} as const;

/**
 * `480` -> `"8:00"`. Sayilar mono + tabular gosterildigi icin dakika iki hane
 * doldurulur. Negatif/gecersiz deger beklenmez; `null` cagirana birakilir
 * (acik kayitta ekran "devam ediyor" gostergesi cizer).
 */
export function formatWorkedMinutes(minutes: number | null | undefined): string | null {
  if (minutes === null || minutes === undefined || !Number.isFinite(minutes)) {
    return null;
  }
  const total = Math.max(0, Math.trunc(minutes));
  const hours = Math.floor(total / 60);
  const rest = total % 60;
  return `${hours}:${String(rest).padStart(2, '0')}`;
}

/** Iki zaman damgasi arasindaki brut dakika; cozumlenemezse `null`. */
export function grossMinutesBetween(
  clockIn: string | null | undefined,
  clockOut: string | null | undefined,
): number | null {
  if (!clockIn || !clockOut) {
    return null;
  }
  const start = new Date(clockIn).getTime();
  const end = new Date(clockOut).getTime();
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return null;
  }
  return Math.floor((end - start) / 60000);
}

/**
 * ISO zaman damgasi -> `<input type="datetime-local">` degeri (**yerel** saat).
 * Sunucu UTC offset'li deger dondurur; kullanici kendi saatini gorur.
 */
export function toDateTimeLocalValue(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  const pad = (part: number): string => String(part).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

/**
 * `<input type="datetime-local">` degeri -> ISO (UTC) zaman damgasi.
 * Deger yerel saat olarak yorumlanir (tarayici davranisi); gecersizse `null`.
 */
export function fromDateTimeLocalValue(value: string | null | undefined): string | null {
  if (!value) {
    return null;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}
