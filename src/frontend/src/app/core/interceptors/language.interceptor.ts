import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { LanguageStore } from '@hotelcore/shared';

/**
 * `Accept-Language: de|en|tr` basligini ekler; backend hata mesajlarini
 * ve icerik cevirilerini bu dile gore dondurur (api-contracts.md).
 */
export const languageInterceptor: HttpInterceptorFn = (request, next) => {
  const language = inject(LanguageStore).acceptLanguageHeader();

  return next(
    request.clone({
      setHeaders: { 'Accept-Language': language },
    }),
  );
};
