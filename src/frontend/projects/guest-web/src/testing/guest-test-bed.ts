import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { REQUEST, type EnvironmentProviders, type Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import {
  provideTranslateService,
  TranslateService,
  type TranslationObject,
} from '@ngx-translate/core';

import { routes } from '../app/app.routes';

/**
 * Testler icin ortak kurulum.
 *
 * Ceviri yukleyicisi BILINCLI olarak baglanmaz: `TranslateNoOpLoader` anahtari
 * oldugu gibi dondurur, boylece testler metin degil **anahtar** dogrular.
 * Metnin kendisi ceviri dosyalarinin isidir (bkz. i18n.spec.ts); bileşen
 * testleri dil dosyasi degisince kirilmamalidir.
 *
 * `acceptLanguage` verilirse SSR'daki `REQUEST` belirteci taklit edilir; boylece
 * dil pazarligi testi calisan makinenin tarayici diline bagli olmaz.
 */
export function configureGuestTestBed(
  options: { acceptLanguage?: string; providers?: (Provider | EnvironmentProviders)[] } = {},
): void {
  const providers: (Provider | EnvironmentProviders)[] = [
    provideRouter(routes, withComponentInputBinding()),
    provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
    /*
     * HTTP her testte SAHTEDIR. Gercek bir istek atilmasi hem yavas hem de
     * sonucu makinenin agina baglar; ayrica public uclar bu turda henuz canli
     * degildir. `HttpTestingController` ile istekler acikca yanitlanir —
     * yanitlanmayanlar testi kirmaz (yalnizca `verify()` cagrilirsa).
     */
    provideHttpClient(),
    provideHttpClientTesting(),
    ...(options.providers ?? []),
  ];

  if (options.acceptLanguage !== undefined) {
    providers.push({
      provide: REQUEST,
      useValue: {
        headers: new Headers({ 'accept-language': options.acceptLanguage }),
      } as unknown as Request,
    });
  }

  harness = null;
  TestBed.configureTestingModule({ providers });
}

/**
 * Testte **birkac** anahtar icin gercek metin tanimlar.
 *
 * Varsayilan kural degismez: bileşen testleri anahtar dogrular, metin degil.
 * Ama parametre enterpolasyonunun dogrulanmasi gereken yerler vardir — ornegin
 * "iptal ucreti tutarla birlikte gosteriliyor mu" sorusu, ancak `{{amount}}`
 * yerine gercek tutar basildiginda yanitlanabilir. Bu yardimci yalnizca o
 * anahtarlar icin iskelet bir sablon verir; ceviri dosyalari degistiginde
 * testler yine kirilmaz.
 */
export function useTestTranslations(
  translations: Record<string, unknown>,
  lang = 'de',
): void {
  TestBed.inject(TranslateService).setTranslation(lang, translations as TranslationObject, true);
}

/*
 * Router harness'i test BASINA bir kez olusturulabilir (Angular kurali). Bu
 * yardimci onu saklar, boylece ayni test icinde birden fazla gezinme
 * yapilabilir — "dil degistiginde ayni sayfada kalir" gibi davranislar ancak
 * ardisik gezinmelerle dogrulanabilir.
 */
let harness: RouterTestingHarness | null = null;

export async function renderRoute(url: string): Promise<HTMLElement> {
  harness ??= await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  return harness.fixture.nativeElement as HTMLElement;
}
