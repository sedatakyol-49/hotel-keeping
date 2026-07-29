import { HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { TokenStorageService } from '../services/token-storage.service';
import { AuthStore } from '../state/auth.store';
import { NotificationStore } from '../state/notification.store';
import { SKIP_AUTH_REDIRECT, SKIP_ERROR_NOTIFICATION } from './http-context.tokens';
import { toApiError } from './problem-details.mapper';

/**
 * Merkezi hata isleme:
 * - `ProblemDetails` -> `ApiError` (i18n anahtarina cevrilir),
 * - 401: yerel oturum temizlenir ve login sayfasina yonlendirilir,
 * - diger hatalar bildirim seridine yazilir (`SKIP_ERROR_NOTIFICATION` ile bastirilabilir).
 *
 * Not: burada `AuthService` yerine `AuthStore`/`TokenStorageService` kullanilir;
 * boylece `HttpClient -> interceptor -> AuthService -> HttpClient` dairesel
 * bagimliligi olusmaz.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationStore);
  const authStore = inject(AuthStore);
  const tokens = inject(TokenStorageService);
  const router = inject(Router);
  const suppressNotification = request.context.get(SKIP_ERROR_NOTIFICATION);
  const suppressRedirect = request.context.get(SKIP_AUTH_REDIRECT);

  return next(request).pipe(
    catchError((error: unknown) => {
      const apiError = toApiError(error);

      if (apiError.status === 401 && !suppressRedirect) {
        tokens.clear();
        authStore.setAnonymous('auth.sessionExpired');
        void router.navigate(['/login'], {
          queryParams: { redirectTo: router.url },
        });
        return throwError(() => error);
      }

      if (!suppressNotification) {
        notifications.push(apiError.messageKey, 'danger', apiError.detail);
      }

      return throwError(() => (error instanceof HttpErrorResponse ? error : apiError));
    }),
  );
};
