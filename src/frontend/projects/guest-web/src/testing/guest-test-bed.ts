import { REQUEST, type EnvironmentProviders, type Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';

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
