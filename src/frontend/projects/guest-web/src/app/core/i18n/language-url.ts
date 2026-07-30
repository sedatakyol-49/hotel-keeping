import {
  DEFAULT_LANGUAGE,
  isAppLanguage,
  normalizeLanguage,
  type AppLanguage,
} from '@hotelcore/shared';

/**
 * Misafir sitesinde dil **URL'de** yasar (`/de/...`, `/en/...`, `/tr/...`).
 *
 * NEDEN localStorage YETMEZ (panelden ayrilan kararin gerekcesi):
 * arama motoru tek bir adresi tek bir icerik olarak gorur. Dil yalnizca
 * tarayici deposunda tutulursa `https://…/` uc dilde uc farkli icerik dondurur;
 * Google bunlardan yalnizca birini (ilk gordugunu) indeksler, `hreflang`
 * kurulamaz ve paylasilan bir baglanti aliciyi yanlis dile dusurur.
 *
 * Bu dosya **saf** fonksiyonlardir: router'a, DOM'a ve DI'ya bagimli degildir;
 * hem tarayicida hem sunucuda (SSR) ayni sonucu verir.
 */

/**
 * Dil gibi gorunen segment (`fr`, `pt-br`, `zh-Hans`). Desteklenmeyen bir dil
 * on eki ile gelen adresleri 404'e dusurmek yerine dogru dile yonlendirmek icin
 * kullanilir.
 */
const LANGUAGE_LIKE_SEGMENT = /^[a-z]{2}(?:-[a-z0-9]{2,8})?$/i;

export interface SplitUrl {
  /** Desteklenen bir dil on eki bulunduysa o dil, yoksa `null`. */
  readonly language: AppLanguage | null;
  /** Dil on eki cikarilmis yol (bas/son egik cizgi olmadan). */
  readonly path: string;
  /** `?query` ve `#fragment` kismi (varsa), oldugu gibi korunur. */
  readonly suffix: string;
}

/** `/en/legal/imprint?a=1#b` -> `{ language: 'en', path: 'legal/imprint', suffix: '?a=1#b' }` */
export function splitLanguagePrefix(url: string): SplitUrl {
  const suffixIndex = firstIndexOf(url, ['?', '#']);
  const suffix = suffixIndex === -1 ? '' : url.slice(suffixIndex);
  const pathname = suffixIndex === -1 ? url : url.slice(0, suffixIndex);

  const segments = pathname.split('/').filter((segment) => segment.length > 0);
  const first = segments[0];

  if (first !== undefined && isAppLanguage(first)) {
    return { language: first, path: segments.slice(1).join('/'), suffix };
  }

  return { language: null, path: segments.join('/'), suffix };
}

/**
 * Adresi verilen dile tasir. Dil secici bunu kullanir: kullanici dili
 * degistirdiginde **ayni sayfada** kalir, ana sayfaya atilmaz.
 */
export function withLanguage(url: string, language: AppLanguage): string {
  const { path, suffix } = splitLanguagePrefix(url);
  return path.length > 0 ? `/${language}/${path}${suffix}` : `/${language}${suffix}`;
}

/**
 * Desteklenmeyen bir dil on eki (`/fr/...`) varsa **atar**, sonra hedef dili
 * onune koyar. `/fr/legal/imprint` + `de` -> `/de/legal/imprint`.
 * Boylece yanlis dilden gelen derin baglantilar icerigi kaybetmez.
 */
export function toLanguageUrl(url: string, language: AppLanguage): string {
  const { language: supported, path, suffix } = splitLanguagePrefix(url);

  if (supported !== null) {
    return withLanguage(url, language);
  }

  const segments = path.split('/').filter((segment) => segment.length > 0);
  const first = segments[0];
  const rest =
    first !== undefined && LANGUAGE_LIKE_SEGMENT.test(first) ? segments.slice(1) : segments;

  return rest.length > 0 ? `/${language}/${rest.join('/')}${suffix}` : `/${language}${suffix}`;
}

/** Dil on ekli mutlak yol uretir: `languagePath('tr', 'legal', 'imprint')` -> `/tr/legal/imprint`. */
export function languagePath(language: AppLanguage, ...segments: readonly string[]): string {
  const tail = segments.filter((segment) => segment.length > 0).join('/');
  return tail.length > 0 ? `/${language}/${tail}` : `/${language}`;
}

/**
 * `Accept-Language` basligini kalite degerine gore siralanmis dil listesine cevirir.
 * `de-DE,de;q=0.9,en;q=0.8` -> `['de-DE', 'de', 'en']`
 */
export function parseAcceptLanguage(header: string | null | undefined): readonly string[] {
  if (!header) {
    return [];
  }

  return header
    .split(',')
    .map((part) => {
      const [tag, ...parameters] = part.trim().split(';');
      const quality = parameters
        .map((parameter) => /^\s*q=([0-9.]+)\s*$/i.exec(parameter))
        .find((match) => match !== null);

      return { tag: tag.trim(), quality: quality ? Number(quality[1]) : 1 };
    })
    .filter((entry) => entry.tag.length > 0 && Number.isFinite(entry.quality) && entry.quality > 0)
    .sort((a, b) => b.quality - a.quality)
    .map((entry) => entry.tag);
}

/** Aday dil etiketlerinden ilk desteklenen dili secer; yoksa `de`. */
export function negotiateLanguage(candidates: readonly string[]): AppLanguage {
  for (const candidate of candidates) {
    const match = normalizeLanguage(candidate);
    if (match) {
      return match;
    }
  }
  return DEFAULT_LANGUAGE;
}

function firstIndexOf(value: string, needles: readonly string[]): number {
  const found = needles.map((needle) => value.indexOf(needle)).filter((index) => index !== -1);
  return found.length > 0 ? Math.min(...found) : -1;
}
