import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type { PagedResult } from '../models/paged-result.model';
import type {
  CreateVacationRequest,
  VacationBalanceQuery,
  VacationBalanceResponse,
  VacationDecisionRequest,
  VacationListQuery,
  VacationRequestResponse,
} from '../models/vacation.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/**
 * Izin uclarinda hatalar cagiran ekranda (liste hata blogu / form alan hatasi /
 * satir aksiyonu) gosterildigi icin global bildirim seridi bastirilir.
 */
function vacationsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/** Bos/null filtreler sorgu dizesine hic eklenmez. */
function toListParams(query: VacationListQuery): HttpParams {
  let params = new HttpParams()
    .set('page', String(query.page))
    .set('pageSize', String(query.pageSize));

  if (query.employeeId) {
    params = params.set('employeeId', query.employeeId);
  }
  if (query.status) {
    params = params.set('status', query.status);
  }
  if (query.year !== null && query.year !== undefined) {
    params = params.set('year', String(query.year));
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
 * `/api/v1/vacations` sozlesmesi (docs/api-contracts.md — Izin).
 * Header yonetimi (`Authorization`, `X-Hotel-Id`, `Accept-Language`)
 * interceptor'lara aittir.
 */
@Injectable({ providedIn: 'root' })
export class VacationsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /vacations` — sayfali + filtreli; `Vacations.View`. */
  list(query: VacationListQuery): Observable<PagedResult<VacationRequestResponse>> {
    return this.http.get<PagedResult<VacationRequestResponse>>(
      joinApiUrl(this.baseUrl, '/vacations'),
      { params: toListParams(query), context: vacationsContext() },
    );
  }

  /** `GET /vacations/{id}` — `Vacations.View`. */
  getById(id: string): Observable<VacationRequestResponse> {
    return this.http.get<VacationRequestResponse>(joinApiUrl(this.baseUrl, `/vacations/${id}`), {
      context: vacationsContext(),
    });
  }

  /** `POST /vacations` — `Vacations.Request`; tarih cakismasinda 409. */
  create(request: CreateVacationRequest): Observable<VacationRequestResponse> {
    return this.http.post<VacationRequestResponse>(
      joinApiUrl(this.baseUrl, '/vacations'),
      request,
      { context: vacationsContext() },
    );
  }

  /** `POST /vacations/{id}/approve` — `Vacations.Approve`; bakiyeden duser. */
  approve(id: string, request: VacationDecisionRequest = {}): Observable<VacationRequestResponse> {
    return this.http.post<VacationRequestResponse>(
      joinApiUrl(this.baseUrl, `/vacations/${id}/approve`),
      request,
      { context: vacationsContext() },
    );
  }

  /** `POST /vacations/{id}/reject` — `Vacations.Approve`; bakiyeyi etkilemez. */
  reject(id: string, request: VacationDecisionRequest = {}): Observable<VacationRequestResponse> {
    return this.http.post<VacationRequestResponse>(
      joinApiUrl(this.baseUrl, `/vacations/${id}/reject`),
      request,
      { context: vacationsContext() },
    );
  }

  /** `POST /vacations/{id}/cancel` — `Vacations.Request`; onayliysa bakiyeyi geri verir. */
  cancel(id: string, request: VacationDecisionRequest = {}): Observable<VacationRequestResponse> {
    return this.http.post<VacationRequestResponse>(
      joinApiUrl(this.baseUrl, `/vacations/${id}/cancel`),
      request,
      { context: vacationsContext() },
    );
  }

  /**
   * `GET /vacations/balances?employeeId=&year=` — **duz dizi** doner
   * (sayfalama yok). `employeeId` verilmezse otelin tum kadrosu gelir;
   * `year` verilmezse sunucunun gecerli yili kullanilir.
   */
  balances(query: VacationBalanceQuery = {}): Observable<readonly VacationBalanceResponse[]> {
    let params = new HttpParams();
    if (query.employeeId) {
      params = params.set('employeeId', query.employeeId);
    }
    if (query.year !== null && query.year !== undefined) {
      params = params.set('year', String(query.year));
    }
    return this.http.get<readonly VacationBalanceResponse[]>(
      joinApiUrl(this.baseUrl, '/vacations/balances'),
      { params, context: vacationsContext() },
    );
  }
}
