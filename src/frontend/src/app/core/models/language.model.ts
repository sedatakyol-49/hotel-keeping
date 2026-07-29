/** Uygulamanin destekledigi diller (mimari dokumani §8). */
export const SUPPORTED_LANGUAGES = ['de', 'en', 'tr'] as const;

export type AppLanguage = (typeof SUPPORTED_LANGUAGES)[number];

/** Varsayilan dil: Almanca. */
export const DEFAULT_LANGUAGE: AppLanguage = 'de';

/** Para/tarih bicimlendirmesi icin BCP-47 locale karsiliklari. */
export const LANGUAGE_LOCALES: Readonly<Record<AppLanguage, string>> = {
  de: 'de-DE',
  en: 'en-GB',
  tr: 'tr-TR',
};

/** Diller icin yazi yonu (ileride RTL eklenirse tek nokta). */
export const LANGUAGE_DIRECTIONS: Readonly<Record<AppLanguage, 'ltr' | 'rtl'>> = {
  de: 'ltr',
  en: 'ltr',
  tr: 'ltr',
};

export function isAppLanguage(value: unknown): value is AppLanguage {
  return typeof value === 'string' && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/**
 * `de-DE`, `tr`, `en-GB` gibi degerleri desteklenen bir dile indirger.
 * Eslesme yoksa `null` doner.
 */
export function normalizeLanguage(value: string | null | undefined): AppLanguage | null {
  if (!value) {
    return null;
  }
  const base = value.trim().toLowerCase().split(/[-_]/)[0];
  return isAppLanguage(base) ? base : null;
}
