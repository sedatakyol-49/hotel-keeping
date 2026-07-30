import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type { PagedResult } from '../models/paged-result.model';
import type {
  HousekeepingBoardResponse,
  RoomListQuery,
  RoomResponse,
  RoomWriteRequest,
  UpdateHousekeepingRequest,
} from '../models/room.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/**
 * Oda uclarinda hatalar cagiran ekranda (liste hata blogu / form alan hatasi)
 * gosterildigi icin global bildirim seridi bastirilir.
 */
function roomsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/** Bos/null filtreler sorgu dizesine hic eklenmez. */
function toListParams(query: RoomListQuery): HttpParams {
  let params = new HttpParams()
    .set('page', String(query.page))
    .set('pageSize', String(query.pageSize));

  if (query.roomTypeId) {
    params = params.set('roomTypeId', query.roomTypeId);
  }
  if (query.floor !== null && query.floor !== undefined) {
    params = params.set('floor', String(query.floor));
  }
  if (query.housekeepingStatus) {
    params = params.set('housekeepingStatus', query.housekeepingStatus);
  }
  const search = query.search?.trim();
  if (search) {
    params = params.set('search', search);
  }
  return params;
}

/**
 * `/api/v1/rooms` sozlesmesi (docs/api-contracts.md — Rooms & Housekeeping).
 * Header yonetimi (`Authorization`, `X-Hotel-Id`, `Accept-Language`)
 * interceptor'lara aittir.
 */
@Injectable({ providedIn: 'root' })
export class RoomsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /rooms` — sayfali + filtreli; `Rooms.View`. */
  list(query: RoomListQuery): Observable<PagedResult<RoomResponse>> {
    return this.http.get<PagedResult<RoomResponse>>(joinApiUrl(this.baseUrl, '/rooms'), {
      params: toListParams(query),
      context: roomsContext(),
    });
  }

  /** `GET /rooms/{id}` — `Rooms.View`. */
  getById(id: string): Observable<RoomResponse> {
    return this.http.get<RoomResponse>(joinApiUrl(this.baseUrl, `/rooms/${id}`), {
      context: roomsContext(),
    });
  }

  /** `POST /rooms` — `Rooms.Manage`; numara cakismasinda 409. */
  create(request: RoomWriteRequest): Observable<RoomResponse> {
    return this.http.post<RoomResponse>(joinApiUrl(this.baseUrl, '/rooms'), request, {
      context: roomsContext(),
    });
  }

  /** `PUT /rooms/{id}` — `Rooms.Manage`. */
  update(id: string, request: RoomWriteRequest): Observable<RoomResponse> {
    return this.http.put<RoomResponse>(joinApiUrl(this.baseUrl, `/rooms/${id}`), request, {
      context: roomsContext(),
    });
  }

  /** `DELETE /rooms/{id}` — soft-delete; gelecek rezervasyon varsa 409. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/rooms/${id}`), {
      context: roomsContext(),
    });
  }

  /** `GET /rooms/board` — kat bazli pano; `Housekeeping.View`. Para alani icermez. */
  board(): Observable<HousekeepingBoardResponse> {
    return this.http.get<HousekeepingBoardResponse>(joinApiUrl(this.baseUrl, '/rooms/board'), {
      context: roomsContext(),
    });
  }

  /** `PATCH /rooms/{id}/housekeeping` — `Housekeeping.Update`. */
  updateHousekeeping(id: string, request: UpdateHousekeepingRequest): Observable<RoomResponse> {
    return this.http.patch<RoomResponse>(
      joinApiUrl(this.baseUrl, `/rooms/${id}/housekeeping`),
      request,
      { context: roomsContext() },
    );
  }
}
