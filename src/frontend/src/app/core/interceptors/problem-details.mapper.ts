import { HttpErrorResponse } from '@angular/common/http';

import {
  isProblemDetails,
  type ApiError,
  type ProblemDetails,
} from '../models/problem-details.model';

/** HTTP durum kodu -> i18n hata anahtari. */
const STATUS_MESSAGE_KEYS: Readonly<Record<number, string>> = {
  0: 'errors.network',
  400: 'errors.badRequest',
  401: 'errors.unauthorized',
  403: 'errors.forbidden',
  404: 'errors.notFound',
  408: 'errors.timeout',
  409: 'errors.conflict',
  422: 'errors.validation',
  500: 'errors.server',
  502: 'errors.server',
  503: 'errors.server',
  504: 'errors.timeout',
};

/**
 * `HttpErrorResponse` -> `ApiError`.
 * Backend RFC 7807 `ProblemDetails` dondurur; `detail`/`errors` alanlari
 * korunur, kullaniciya gosterilecek metin ise i18n anahtarina cevrilir.
 */
export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, messageKey: 'errors.unknown' };
  }

  if (!globalThis.navigator?.onLine) {
    return { status: 0, messageKey: 'errors.offline' };
  }

  const problem: ProblemDetails | null = isProblemDetails(error.error) ? error.error : null;
  const status = problem?.status ?? error.status ?? 0;
  const hasFieldErrors = !!problem?.errors && Object.keys(problem.errors).length > 0;

  return {
    status,
    messageKey: hasFieldErrors
      ? 'errors.validation'
      : (STATUS_MESSAGE_KEYS[status] ?? 'errors.unknown'),
    detail: problem?.detail ?? problem?.title ?? undefined,
    fieldErrors: problem?.errors,
    traceId: problem?.traceId,
  };
}

/** Alan bazli dogrulama hatalarini duz bir listeye cevirir. */
export function flattenFieldErrors(error: ApiError): readonly string[] {
  if (!error.fieldErrors) {
    return [];
  }
  return Object.values(error.fieldErrors).flat();
}
