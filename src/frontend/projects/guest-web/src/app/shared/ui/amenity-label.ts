import type { TranslateService } from '@ngx-translate/core';

/**
 * Donanim anahtari -> gorunur ad.
 *
 * API donanimlari **anahtar** olarak dondurur (`wifi`, `minibar`, `balcony`);
 * cevrilmis metin istemcinin isidir. Katalogda karsilik yoksa anahtarin
 * kendisi gosterilir: bos birakmak (donanimi gizlemek) yanlis olur, cunku
 * odanin bir ozelligi kaybolur; ceviri eksigi ise gorunur kalmalidir.
 */
export function amenityLabel(translate: TranslateService, amenity: string): string {
  const key = `amenities.${amenity}`;
  const value: unknown = translate.instant(key);
  return typeof value === 'string' && value !== key ? value : amenity;
}
