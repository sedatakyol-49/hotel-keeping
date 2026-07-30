import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type {
  AvailabilityQuery,
  AvailabilityResponse,
  OccupancyQuery,
  OccupancyResponse,
} from '../models/availability.model';
import type { PagedResult } from '../models/paged-result.model';
import type {
  CancelReservationRequest,
  CreateReservationRequest,
  FolioResponse,
  ReservationListQuery,
  ReservationResponse,
  UpdateReservationRequest,
} from '../models/reservation.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/**
 * Rezervasyon uclarinda hatalar cagiran ekranda (409 cakisma metni, alan
 * hatasi, aksiyon hata blogu) gosterildigi icin global bildirim bastirilir.
 */
function reservationsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/** Bos/null filtreler sorgu dizesine hic eklenmez. */
function toListParams(query: ReservationListQuery): HttpParams {
  let params = new HttpParams()
    .set('page', String(query.page))
    .set('pageSize', String(query.pageSize));

  if (query.status) {
    params = params.set('status', query.status);
  }
  if (query.channel) {
    params = params.set('channel', query.channel);
  }
  if (query.roomId) {
    params = params.set('roomId', query.roomId);
  }
  if (query.guestId) {
    params = params.set('guestId', query.guestId);
  }
  if (query.from) {
    params = params.set('from', query.from);
  }
  if (query.to) {
    params = params.set('to', query.to);
  }
  const search = query.search?.trim();
  if (search) {
    params = params.set('search', search);
  }
  return params;
}

/**
 * `/api/v1/reservations` + `/availability` + `/occupancy` sozlesmesi
 * (docs/api-contracts-reservations.md).
 *
 * Header yonetimi (`Authorization`, `X-Hotel-Id`, `Accept-Language`)
 * interceptor'lara aittir.
 */
@Injectable({ providedIn: 'root' })
export class ReservationsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /reservations` — sayfali + filtreli; `Reservations.View`. */
  list(query: ReservationListQuery): Observable<PagedResult<ReservationResponse>> {
    return this.http.get<PagedResult<ReservationResponse>>(
      joinApiUrl(this.baseUrl, '/reservations'),
      { params: toListParams(query), context: reservationsContext() },
    );
  }

  /** `GET /reservations/{id}` — `Reservations.View`. */
  getById(id: string): Observable<ReservationResponse> {
    return this.http.get<ReservationResponse>(joinApiUrl(this.baseUrl, `/reservations/${id}`), {
      context: reservationsContext(),
    });
  }

  /**
   * `POST /reservations` — `Reservations.Create`; 201 + `Location`.
   *
   * **`totalAmount` gonderilmez**: tutar sunucuda gece gece hesaplanir ve
   * yanitta doner (`totalAmount`, `ratePlanName`). Tarih cakismasinda **409**
   * ve `detail` hangi rezervasyonla cakisildigini soyler.
   */
  create(request: CreateReservationRequest): Observable<ReservationResponse> {
    return this.http.post<ReservationResponse>(
      joinApiUrl(this.baseUrl, '/reservations'),
      request,
      { context: reservationsContext() },
    );
  }

  /**
   * `PUT /reservations/{id}` — `Reservations.Create`.
   * `status` **tasinmaz**; musaitlik ve tutar yeniden hesaplanir.
   */
  update(id: string, request: UpdateReservationRequest): Observable<ReservationResponse> {
    return this.http.put<ReservationResponse>(
      joinApiUrl(this.baseUrl, `/reservations/${id}`),
      request,
      { context: reservationsContext() },
    );
  }

  /** `POST /reservations/{id}/check-in` — `Reservations.CheckInOut`. */
  checkIn(id: string): Observable<ReservationResponse> {
    return this.http.post<ReservationResponse>(
      joinApiUrl(this.baseUrl, `/reservations/${id}/check-in`),
      {},
      { context: reservationsContext() },
    );
  }

  /**
   * `POST /reservations/{id}/check-out` — `Reservations.CheckInOut`.
   * Oda `housekeepingStatus` degeri **otomatik `Dirty`** olur (ayni transaction).
   */
  checkOut(id: string): Observable<ReservationResponse> {
    return this.http.post<ReservationResponse>(
      joinApiUrl(this.baseUrl, `/reservations/${id}/check-out`),
      {},
      { context: reservationsContext() },
    );
  }

  /** `POST /reservations/{id}/cancel` — `Reservations.Create`; govde opsiyonel. */
  cancel(id: string, request: CancelReservationRequest = {}): Observable<ReservationResponse> {
    return this.http.post<ReservationResponse>(
      joinApiUrl(this.baseUrl, `/reservations/${id}/cancel`),
      request,
      { context: reservationsContext() },
    );
  }

  /** `POST /reservations/{id}/no-show` — `Reservations.CheckInOut`. */
  noShow(id: string): Observable<ReservationResponse> {
    return this.http.post<ReservationResponse>(
      joinApiUrl(this.baseUrl, `/reservations/${id}/no-show`),
      {},
      { context: reservationsContext() },
    );
  }

  /** `GET /reservations/{id}/folio` — acik hesap satirlari + toplamlar. */
  folio(id: string): Observable<FolioResponse> {
    return this.http.get<FolioResponse>(joinApiUrl(this.baseUrl, `/reservations/${id}/folio`), {
      context: reservationsContext(),
    });
  }

  /**
   * `GET /availability?from=&to=&roomTypeId=` — `Reservations.View`.
   * Yanitta **fiyat alani yoktur**; aralik en fazla 366 gundur.
   */
  availability(query: AvailabilityQuery): Observable<AvailabilityResponse> {
    let params = new HttpParams().set('from', query.from).set('to', query.to);
    if (query.roomTypeId) {
      params = params.set('roomTypeId', query.roomTypeId);
    }
    return this.http.get<AvailabilityResponse>(joinApiUrl(this.baseUrl, '/availability'), {
      params,
      context: reservationsContext(),
    });
  }

  /**
   * `GET /occupancy?from=&to=` — **oda × gun** matrisi; `Reservations.View`.
   * `cells` seyrektir (yalnizca dolu geceler) ve aralik en fazla **92 gun**dur
   * — istemci sinirini asan istegi hic gondermez.
   */
  occupancy(query: OccupancyQuery): Observable<OccupancyResponse> {
    const params = new HttpParams().set('from', query.from).set('to', query.to);
    return this.http.get<OccupancyResponse>(joinApiUrl(this.baseUrl, '/occupancy'), {
      params,
      context: reservationsContext(),
    });
  }
}
