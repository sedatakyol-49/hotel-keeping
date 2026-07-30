import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type { PagedResult } from '../models/paged-result.model';
import type {
  ClockInRequest,
  ClockOutRequest,
  TimeEntryListQuery,
  TimeEntryResponse,
  UpdateTimeEntryRequest,
} from '../models/time-entry.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/** Hatalar cagiran ekranda gosterildigi icin global bildirim seridi bastirilir. */
function timeEntriesContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/** Bos/null filtreler sorgu dizesine hic eklenmez. */
function toListParams(query: TimeEntryListQuery): HttpParams {
  let params = new HttpParams()
    .set('page', String(query.page))
    .set('pageSize', String(query.pageSize));

  if (query.employeeId) {
    params = params.set('employeeId', query.employeeId);
  }
  if (query.from) {
    params = params.set('from', query.from);
  }
  if (query.to) {
    params = params.set('to', query.to);
  }
  return params;
}

/**
 * `/api/v1/time-entries` sozlesmesi (docs/api-contracts.md — Zeiterfassung).
 *
 * Not: sozlesmede **tek kayit okuma ucu yoktur** (`GET /time-entries/{id}`
 * yok); manuel duzeltme bu yuzden listedeki satirdan beslenir.
 */
@Injectable({ providedIn: 'root' })
export class TimeEntriesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /time-entries` — sayfali + filtreli; `TimeTracking.View`. */
  list(query: TimeEntryListQuery): Observable<PagedResult<TimeEntryResponse>> {
    return this.http.get<PagedResult<TimeEntryResponse>>(
      joinApiUrl(this.baseUrl, '/time-entries'),
      { params: toListParams(query), context: timeEntriesContext() },
    );
  }

  /** `POST /time-entries/clock-in` — `TimeTracking.Record`; acik kayit varsa 409. */
  clockIn(request: ClockInRequest): Observable<TimeEntryResponse> {
    return this.http.post<TimeEntryResponse>(
      joinApiUrl(this.baseUrl, '/time-entries/clock-in'),
      request,
      { context: timeEntriesContext() },
    );
  }

  /** `POST /time-entries/clock-out` — `TimeTracking.Record`; acik kayit yoksa 409. */
  clockOut(request: ClockOutRequest): Observable<TimeEntryResponse> {
    return this.http.post<TimeEntryResponse>(
      joinApiUrl(this.baseUrl, '/time-entries/clock-out'),
      request,
      { context: timeEntriesContext() },
    );
  }

  /** `PUT /time-entries/{id}` — manuel duzeltme; `TimeTracking.Record`. */
  update(id: string, request: UpdateTimeEntryRequest): Observable<TimeEntryResponse> {
    return this.http.put<TimeEntryResponse>(
      joinApiUrl(this.baseUrl, `/time-entries/${id}`),
      request,
      { context: timeEntriesContext() },
    );
  }

  /** `DELETE /time-entries/{id}` — `TimeTracking.Record`. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/time-entries/${id}`), {
      context: timeEntriesContext(),
    });
  }
}
