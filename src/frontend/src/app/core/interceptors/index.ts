import type { HttpInterceptorFn } from '@angular/common/http';

import { authInterceptor } from './auth.interceptor';
import { errorInterceptor } from './error.interceptor';
import { hotelContextInterceptor } from './hotel-context.interceptor';
import { languageInterceptor } from './language.interceptor';

export * from './auth.interceptor';
export * from './error.interceptor';
export * from './hotel-context.interceptor';
export * from './http-context.tokens';
export * from './language.interceptor';
export * from './problem-details.mapper';

/**
 * Sira onemlidir: hata yakalayici en distadir, boylece kendisinden sonraki
 * tum interceptor'larin ve backend'in urettigi hatalari gorur.
 */
export const HTTP_INTERCEPTORS_IN_ORDER: readonly HttpInterceptorFn[] = [
  errorInterceptor,
  authInterceptor,
  hotelContextInterceptor,
  languageInterceptor,
];
