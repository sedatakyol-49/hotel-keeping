import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { TokenStorageService } from '../services/token-storage.service';
import { SKIP_AUTH_HEADER } from './http-context.tokens';

/**
 * `Authorization: Bearer <jwt>` basligini ekler.
 * Login/refresh gibi anonim cagrilarda `SKIP_AUTH_HEADER` ile atlanir.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(SKIP_AUTH_HEADER)) {
    return next(request);
  }

  const accessToken = inject(TokenStorageService).accessToken();
  if (!accessToken) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { Authorization: `Bearer ${accessToken}` },
    }),
  );
};
