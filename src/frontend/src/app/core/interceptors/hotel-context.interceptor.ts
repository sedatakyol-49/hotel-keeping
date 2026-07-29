import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { CurrentHotelService } from '../services/current-hotel.service';
import { SKIP_HOTEL_CONTEXT } from './http-context.tokens';

/**
 * Aktif otel baglamini `X-Hotel-Id` basligi ile gonderir.
 *
 * Baslik yoksa backend JWT'deki varsayilan oteli kullanir; Head Office
 * kullanicisi otel secmediginde (konsolide gorunum) baslik bilincli olarak
 * gonderilmez (api-contracts.md — Genel Kurallar).
 */
export const hotelContextInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.context.get(SKIP_HOTEL_CONTEXT)) {
    return next(request);
  }

  const hotelId = inject(CurrentHotelService).hotelId();
  if (!hotelId) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { 'X-Hotel-Id': hotelId },
    }),
  );
};
