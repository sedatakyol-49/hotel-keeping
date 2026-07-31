import type { HttpInterceptorFn } from '@angular/common/http';
import { REQUEST, inject } from '@angular/core';

import { LanguageStore } from '@hotelcore/shared';

/**
 * SSR'da GORELI ADRES SORUNU.
 *
 * Tarayicida `/api/v1/...` adresi belge kokune gore cozulur. Sunucuda boyle bir
 * "belge" yoktur; `fetch` mutlak adres ister ve goreli istek "Failed to parse
 * URL" ile duser. Arama sonuclari ve oda tipi sayfalari **sunucuda** render
 * edildigi icin (bkz. app.routes.server.ts) bu yol mutlaka calismalidir.
 *
 * Cozum: sunucuda calisirken istegin **kendi origin'i** one eklenir. Boylece
 * yapilandirmada tek bir goreli taban adres (`/api/v1`) yeter; ayrica ters
 * vekil arkasindaki dagitimda otomatik olarak dogru host kullanilir.
 * Mutlak adresler (test/mock) oldugu gibi birakilir.
 */
export const apiUrlInterceptor: HttpInterceptorFn = (request, next) => {
  if (/^[a-z][a-z0-9+.-]*:/i.test(request.url) || !request.url.startsWith('/')) {
    return next(request);
  }

  const incoming = inject(REQUEST, { optional: true });
  const origin = originOf(incoming?.url);
  if (origin === null) {
    return next(request);
  }

  return next(request.clone({ url: `${origin}${request.url}` }));
};

/**
 * `REQUEST` her zaman tam bir istek olmayabilir (testlerde taklit edilir).
 * Cozulemeyen bir adres varsa istek **degistirilmeden** gecer; sunucuda hata
 * uretmektense goreli adresle devam etmek daha az zararlidir.
 */
function originOf(url: string | undefined): string | null {
  if (typeof url !== 'string' || url.length === 0) {
    return null;
  }
  try {
    return new URL(url).origin;
  } catch {
    return null;
  }
}

/**
 * `Accept-Language` — sozlesme §1: cok dilli icerik (oda tipi adi, hukuki
 * metin) bu basliga gore cozulur. Dilin tek kaynagi URL on ekidir
 * (`LanguageStore`); baslik da oradan turer, ikinci bir tercih deposu yoktur.
 */
export const acceptLanguageInterceptor: HttpInterceptorFn = (request, next) => {
  const language = inject(LanguageStore).acceptLanguageHeader();
  return next(request.clone({ setHeaders: { 'Accept-Language': language } }));
};
