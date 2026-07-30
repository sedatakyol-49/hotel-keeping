import { DOCUMENT, Injectable, REQUEST, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Observable, map, of, switchMap } from 'rxjs';

import { LanguageStore, isAppLanguage, type AppLanguage } from '@hotelcore/shared';

import { negotiateLanguage, parseAcceptLanguage } from './language-url';

/**
 * Misafir sitesinin dil politikasi.
 *
 * PANELDEN FARKI (bilincli): panelde dil bir **kullanici tercihidir** ve
 * `localStorage`'da yasar. Burada dil **adresin bir parcasidir**; tek dogru
 * kaynak URL'dir. Yerel depoya yazilmaz — aksi halde `/en/...` adresini acan
 * biri, daha once `de` sectigi icin Almanca icerik gorurdu; bu hem kullaniciyi
 * hem tarayiciyi (crawler) sasirtir ve `hreflang` sozunu bozar.
 *
 * `LanguageStore` (durum) paylasilan katmandan gelir; degisen yalnizca
 * durumun **nasil** belirlendigidir.
 */
@Injectable({ providedIn: 'root' })
export class GuestLanguageService {
  private readonly translate = inject(TranslateService);
  private readonly store = inject(LanguageStore);
  private readonly document = inject(DOCUMENT);
  /** SSR sirasinda gelen istek; tarayicida `null`. */
  private readonly request = inject(REQUEST, { optional: true });

  /**
   * Rota `:lang` segmentini uygular ve **cevirilerin yuklenmesini bekler**.
   * Guard bu Observable'i dondurdugu icin Angular, ceviriler hazir olmadan
   * sayfayi render etmez — SSR ciktisinda anahtar degil, metin bulunur.
   */
  activate(language: AppLanguage): Observable<boolean> {
    this.store.set(language);
    this.document.documentElement.lang = language;
    this.document.documentElement.dir = this.store.direction();

    if (this.translate.currentLang() === language) {
      return of(true);
    }

    return this.translate.use(language).pipe(map(() => true));
  }

  /** Ceviriler yuklendikten sonra bir anahtari cozer (baslik/aciklama icin). */
  instant(key: string): Observable<string> {
    return this.translate.get(key).pipe(switchMap((value: string) => of(value)));
  }

  /**
   * Dil on eki olmayan adresler icin dil pazarligi.
   * Sunucuda `Accept-Language`, tarayicida `navigator.languages`; ikisi de
   * yoksa `de`. Sonuc her zaman bir **yonlendirmedir**; dil on eksiz bir sayfa
   * asla servis edilmez (kanonik adres tek olmalidir).
   */
  negotiate(): AppLanguage {
    const fromHeader = parseAcceptLanguage(this.request?.headers.get('accept-language'));
    if (fromHeader.length > 0) {
      return negotiateLanguage(fromHeader);
    }

    const navigatorLanguages = globalThis.navigator?.languages ?? [];
    return negotiateLanguage(
      navigatorLanguages.length > 0
        ? navigatorLanguages
        : [globalThis.navigator?.language ?? ''].filter((value) => value.length > 0),
    );
  }

  /** Rota parametresini dogrular. */
  static parse(value: string | null): AppLanguage | null {
    return value !== null && isAppLanguage(value) ? value : null;
  }
}
