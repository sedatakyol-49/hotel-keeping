import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type { ShiftPlanResponse, ShiftResponse, ShiftWriteRequest } from '../models/shift.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/** Hatalar cagiran ekranda gosterildigi icin global bildirim seridi bastirilir. */
function shiftsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/**
 * `/api/v1/shifts` sozlesmesi (docs/api-contracts.md — Vardiya).
 *
 * `week` ve `from`/`to` birlikte gonderilmez: sozlesme geregi `week`
 * digerlerini gecersiz kilar, bu yuzden iki ayri yordam vardir.
 */
@Injectable({ providedIn: 'root' })
export class ShiftsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /shifts?week=YYYY-Www` — `Shifts.View`. */
  planByWeek(week: string): Observable<ShiftPlanResponse> {
    return this.http.get<ShiftPlanResponse>(joinApiUrl(this.baseUrl, '/shifts'), {
      params: new HttpParams().set('week', week),
      context: shiftsContext(),
    });
  }

  /** `GET /shifts?from=&to=` — `Shifts.View` (serbest aralik). */
  planByRange(from: string, to: string): Observable<ShiftPlanResponse> {
    return this.http.get<ShiftPlanResponse>(joinApiUrl(this.baseUrl, '/shifts'), {
      params: new HttpParams().set('from', from).set('to', to),
      context: shiftsContext(),
    });
  }

  /** `POST /shifts` — `Shifts.Edit`; ayni gune ikinci vardiyada 409. */
  create(request: ShiftWriteRequest): Observable<ShiftResponse> {
    return this.http.post<ShiftResponse>(joinApiUrl(this.baseUrl, '/shifts'), request, {
      context: shiftsContext(),
    });
  }

  /** `PUT /shifts/{id}` — `Shifts.Edit`. */
  update(id: string, request: ShiftWriteRequest): Observable<ShiftResponse> {
    return this.http.put<ShiftResponse>(joinApiUrl(this.baseUrl, `/shifts/${id}`), request, {
      context: shiftsContext(),
    });
  }

  /** `DELETE /shifts/{id}` — `Shifts.Edit`. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/shifts/${id}`), {
      context: shiftsContext(),
    });
  }
}
