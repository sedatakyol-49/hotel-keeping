import { LANGUAGE_LOCALES, type AppLanguage } from '../i18n/language.model';

/**
 * ===========================================================================
 * BICIMLENDIRME — para, tarih, sayi
 * ===========================================================================
 *
 * Paylasilan katmanda durur (mimari §2.1): panel ve misafir sitesi ayni sayiyi
 * ayni bicimde gostermek zorundadir. `de-DE` icin `468,00 €`, `en-GB` icin
 * `€468.00`, `tr-TR` icin `468,00 €` — ayrisirsa ayni rezervasyon iki ekranda
 * iki farkli tutar gibi okunur.
 *
 * `Intl` dogrudan kullanilir (bagimlilik yok). Bicimleyiciler onbelleklenir:
 * `Intl.NumberFormat` olusturmak pahalidir ve arama sonuclarinda dizi basina
 * onlarca kez cagrilir.
 */

const numberFormatters = new Map<string, Intl.NumberFormat>();
const dateFormatters = new Map<string, Intl.DateTimeFormat>();

function currencyFormatter(locale: string, currency: string): Intl.NumberFormat {
  const key = `${locale}|${currency}`;
  let formatter = numberFormatters.get(key);
  if (formatter === undefined) {
    formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
    numberFormatters.set(key, formatter);
  }
  return formatter;
}

function dateFormatter(locale: string, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  const key = `${locale}|${JSON.stringify(options)}`;
  let formatter = dateFormatters.get(key);
  if (formatter === undefined) {
    formatter = new Intl.DateTimeFormat(locale, options);
    dateFormatters.set(key, formatter);
  }
  return formatter;
}

export function localeOf(language: AppLanguage): string {
  return LANGUAGE_LOCALES[language];
}

/** `468` + `EUR` + `de` -> `468,00 €`. Tutar her zaman iki ondalikla gosterilir. */
export function formatMoney(
  amount: number,
  currency: string,
  language: AppLanguage,
): string {
  return currencyFormatter(localeOf(language), currency).format(amount);
}

/**
 * `yyyy-MM-dd` -> yerellestirilmis tarih.
 * ONEMLI: dize `new Date(...)` ile ayristirilmaz — `2026-08-10` UTC gece
 * yarisi olarak okunur ve negatif offsetli bir tarayicida BIR GUN GERI kayar.
 * Bunun yerine parcalar elle ayrilir ve **yerel** bir tarih kurulur.
 */
export function formatIsoDate(
  isoDate: string,
  language: AppLanguage,
  options: Intl.DateTimeFormatOptions = { day: '2-digit', month: 'short', year: 'numeric' },
): string {
  const date = parseIsoDate(isoDate);
  return date === null ? isoDate : dateFormatter(localeOf(language), options).format(date);
}

/** `2026-08-10` -> yerel `Date` (saat 12:00; DST kaymalarina karsi guvenli). */
export function parseIsoDate(isoDate: string): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(isoDate);
  if (match === null) {
    return null;
  }
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]), 12);
}

/** `2026-08-10` + `2026-08-13` -> `10. Aug. 2026 – 13. Aug. 2026`. */
export function formatDateRange(
  fromIso: string,
  toIso: string,
  language: AppLanguage,
): string {
  return `${formatIsoDate(fromIso, language)} – ${formatIsoDate(toIso, language)}`;
}

/** Mutlak an (`2026-08-07T18:00:00+02:00`) -> yerellestirilmis tarih + saat. */
export function formatInstant(instant: string, language: AppLanguage): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) {
    return instant;
  }
  return dateFormatter(localeOf(language), {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

/** Yuzde degeri (`90` -> `%90` / `90 %`). */
export function formatPercent(value: number, language: AppLanguage): string {
  return new Intl.NumberFormat(localeOf(language), {
    style: 'percent',
    maximumFractionDigits: 2,
  }).format(value / 100);
}

/** Saniye -> `mm:ss` (geri sayim). Negatif deger `00:00` olur. */
export function formatCountdown(totalSeconds: number): string {
  const safe = Math.max(0, Math.floor(totalSeconds));
  const minutes = Math.floor(safe / 60);
  const seconds = safe % 60;
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}
