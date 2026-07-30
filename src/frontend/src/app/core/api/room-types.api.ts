import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type { RoomTypeResponse, RoomTypeWriteRequest } from '../models/room-type.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/** Hatalar ekranin kendi hata blogunda gosterilir; global serit bastirilir. */
function roomTypesContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/**
 * `/api/v1/room-types` sozlesmesi (docs/api-contracts.md — Rooms & Housekeeping).
 * Oda tipi sayisi az oldugu icin liste **duz dizi** doner, sayfalama yoktur.
 */
@Injectable({ providedIn: 'root' })
export class RoomTypesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /room-types` — `Rooms.View`; ceviri sozlugu **donmez**. */
  list(): Observable<readonly RoomTypeResponse[]> {
    return this.http.get<readonly RoomTypeResponse[]>(joinApiUrl(this.baseUrl, '/room-types'), {
      context: roomTypesContext(),
    });
  }

  /** `GET /room-types/{id}` — `Rooms.View`; **tum** ceviriler `translations` altinda gelir. */
  getById(id: string): Observable<RoomTypeResponse> {
    return this.http.get<RoomTypeResponse>(joinApiUrl(this.baseUrl, `/room-types/${id}`), {
      context: roomTypesContext(),
    });
  }

  /** `POST /room-types` — `Rooms.Manage`; kod cakismasinda 409. */
  create(request: RoomTypeWriteRequest): Observable<RoomTypeResponse> {
    return this.http.post<RoomTypeResponse>(joinApiUrl(this.baseUrl, '/room-types'), request, {
      context: roomTypesContext(),
    });
  }

  /** `PUT /room-types/{id}` — `Rooms.Manage`. */
  update(id: string, request: RoomTypeWriteRequest): Observable<RoomTypeResponse> {
    return this.http.put<RoomTypeResponse>(joinApiUrl(this.baseUrl, `/room-types/${id}`), request, {
      context: roomTypesContext(),
    });
  }

  /** `DELETE /room-types/{id}` — soft-delete; bagli oda varsa 409. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/room-types/${id}`), {
      context: roomTypesContext(),
    });
  }
}
