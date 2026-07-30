import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type {
  RatePlanListQuery,
  RatePlanResponse,
  RatePlanWriteRequest,
} from '../models/rate-plan.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

function ratePlansContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/**
 * `/api/v1/rate-plans` sozlesmesi
 * (docs/api-contracts-reservations.md → Rate Plans).
 *
 * Izinler: okuma `Rates.View`, yazma `Rates.Manage`.
 * Liste **duz dizi** doner (plan sayisi az; sayfalama yok).
 */
@Injectable({ providedIn: 'root' })
export class RatePlansApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /rate-plans?roomTypeId=&date=`. */
  list(query: RatePlanListQuery = {}): Observable<readonly RatePlanResponse[]> {
    let params = new HttpParams();
    if (query.roomTypeId) {
      params = params.set('roomTypeId', query.roomTypeId);
    }
    if (query.date) {
      params = params.set('date', query.date);
    }
    return this.http.get<readonly RatePlanResponse[]>(
      joinApiUrl(this.baseUrl, '/rate-plans'),
      { params, context: ratePlansContext() },
    );
  }

  /** `GET /rate-plans/{id}`. */
  getById(id: string): Observable<RatePlanResponse> {
    return this.http.get<RatePlanResponse>(joinApiUrl(this.baseUrl, `/rate-plans/${id}`), {
      context: ratePlansContext(),
    });
  }

  /**
   * `POST /rate-plans` — 201 + `Location`.
   * Ayni `(roomTypeId, channel)` icin tarih araligi kesisen ikinci **aktif**
   * plan **409** doner; sunucunun `detail` metni cakisan planin adini/araligini
   * verir ve ekran bunu oldugu gibi gosterir.
   */
  create(request: RatePlanWriteRequest): Observable<RatePlanResponse> {
    return this.http.post<RatePlanResponse>(joinApiUrl(this.baseUrl, '/rate-plans'), request, {
      context: ratePlansContext(),
    });
  }

  /** `PUT /rate-plans/{id}` — cakisma kontrolu kendisi haric tutularak yapilir. */
  update(id: string, request: RatePlanWriteRequest): Observable<RatePlanResponse> {
    return this.http.put<RatePlanResponse>(
      joinApiUrl(this.baseUrl, `/rate-plans/${id}`),
      request,
      { context: ratePlansContext() },
    );
  }

  /**
   * `DELETE /rate-plans/{id}` — **hard delete** (soft-delete edilebilir degil).
   * Plana bagli rezervasyon varsa **409**: cozum plani pasife almaktir
   * (`isActive: false`).
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/rate-plans/${id}`), {
      context: ratePlansContext(),
    });
  }
}
