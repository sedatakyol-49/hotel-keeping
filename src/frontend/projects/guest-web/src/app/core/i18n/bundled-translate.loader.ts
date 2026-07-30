import { Injectable } from '@angular/core';
import { TranslateLoader, type TranslationObject } from '@ngx-translate/core';
import { Observable, from, map } from 'rxjs';

import { DEFAULT_LANGUAGE, isAppLanguage, type AppLanguage } from '@hotelcore/shared';

/**
 * Ceviri yukleyici — HTTP **degil**, paket icinden.
 *
 * NEDEN panelden farkli:
 * Panel `TranslateHttpLoader` kullanir; tarayicida calisir, ilk boyamadan sonra
 * metinleri doldurur. Misafir sitesi sunucuda render edilir ve **SEO'nun tum
 * mesele oldugu** yer burasidir: sunucudan cikan HTML'de metin yoksa, arama
 * motorunun gordugu sey bos bir iskelettir. Sunucuda goreli bir `/i18n/de.json`
 * istegi ayrica cozulemez (mutlak adres gerekir) ve her istekte ek bir ag
 * gidis-donusu ekler.
 *
 * Bu yuzden ceviriler derleme zamaninda **paketlenir**. Dinamik `import()`
 * kullanildigi icin her dil ayri bir parcaya (chunk) duser: misafir yalnizca
 * kendi dilini indirir, uc dili birden degil.
 */
const BUNDLES: Readonly<Record<AppLanguage, () => Promise<{ default: TranslationObject }>>> = {
  de: () => import('../../../i18n/de.json'),
  en: () => import('../../../i18n/en.json'),
  tr: () => import('../../../i18n/tr.json'),
};

@Injectable()
export class BundledTranslateLoader extends TranslateLoader {
  getTranslation(lang: string): Observable<TranslationObject> {
    const language = isAppLanguage(lang) ? lang : DEFAULT_LANGUAGE;
    return from(BUNDLES[language]()).pipe(map((module) => module.default));
  }
}
