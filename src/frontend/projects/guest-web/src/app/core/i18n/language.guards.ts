import { inject } from '@angular/core';
import { Router, type CanActivateFn, type CanMatchFn } from '@angular/router';

import { isAppLanguage } from '@hotelcore/shared';

import { GuestLanguageService } from './guest-language.service';
import { languagePath, toLanguageUrl } from './language-url';

/**
 * `:lang` rotasi **yalnizca** desteklenen bir dil segmentiyle eslesir.
 * `canMatch` (canActivate degil) kullanilmasinin sebebi: eslesme basarisiz
 * oldugunda router bir sonraki rotayi (`**`) denemeye devam eder, boylece
 * `/fr/zimmer` 404 yerine dil pazarligina duser.
 */
export const supportedLanguageMatch: CanMatchFn = (_route, segments) => {
  const first = segments[0]?.path;
  return first !== undefined && isAppLanguage(first);
};

/**
 * Dil on ekini uygular ve **cevirilerin yuklenmesini bekler**. Sunucu render'i
 * bu guard tamamlanmadan HTML uretmez; ciktida cevrilmis metin bulunur.
 */
export const languageRouteGuard: CanActivateFn = (route) => {
  const service = inject(GuestLanguageService);
  const language = GuestLanguageService.parse(route.paramMap.get('lang'));

  if (language === null) {
    return inject(Router).parseUrl(languagePath(service.negotiate()));
  }

  return service.activate(language);
};

/**
 * Dil on eki olmayan (veya desteklenmeyen bir on ek tasiyan) her adres, dogru
 * dile **yonlendirilir**. Boylece her icerigin tek bir kanonik adresi olur.
 */
export const languageNegotiationGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const service = inject(GuestLanguageService);

  return router.parseUrl(toLanguageUrl(state.url, service.negotiate()));
};
