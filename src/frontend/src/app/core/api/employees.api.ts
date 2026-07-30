import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type {
  EmployeeListQuery,
  EmployeeResponse,
  EmployeeWriteRequest,
} from '../models/employee.model';
import type { PagedResult } from '../models/paged-result.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/**
 * Personel uclarinda hatalar cagiran ekranda (liste hata blogu / form alan
 * hatasi) gosterildigi icin global bildirim seridi bastirilir.
 */
function employeesContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/**
 * Bos/null filtreler sorgu dizesine hic eklenmez; `includeTerminated`
 * yalnizca `true` iken gonderilir (sunucu varsayilani `false`).
 */
function toListParams(query: EmployeeListQuery): HttpParams {
  let params = new HttpParams()
    .set('page', String(query.page))
    .set('pageSize', String(query.pageSize));

  if (query.departmentId) {
    params = params.set('departmentId', query.departmentId);
  }
  if (query.employmentType) {
    params = params.set('employmentType', query.employmentType);
  }
  const search = query.search?.trim();
  if (search) {
    params = params.set('search', search);
  }
  if (query.includeTerminated) {
    params = params.set('includeTerminated', 'true');
  }
  return params;
}

/**
 * `/api/v1/employees` sozlesmesi (docs/api-contracts.md — Personel).
 * Header yonetimi (`Authorization`, `X-Hotel-Id`, `Accept-Language`)
 * interceptor'lara aittir.
 */
@Injectable({ providedIn: 'root' })
export class EmployeesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /employees` — sayfali + filtreli; `Employees.View`. */
  list(query: EmployeeListQuery): Observable<PagedResult<EmployeeResponse>> {
    return this.http.get<PagedResult<EmployeeResponse>>(joinApiUrl(this.baseUrl, '/employees'), {
      params: toListParams(query),
      context: employeesContext(),
    });
  }

  /** `GET /employees/{id}` — `Employees.View`. */
  getById(id: string): Observable<EmployeeResponse> {
    return this.http.get<EmployeeResponse>(joinApiUrl(this.baseUrl, `/employees/${id}`), {
      context: employeesContext(),
    });
  }

  /** `POST /employees` — `Employees.Edit`; `staffNumber` cakismasinda 409. */
  create(request: EmployeeWriteRequest): Observable<EmployeeResponse> {
    return this.http.post<EmployeeResponse>(joinApiUrl(this.baseUrl, '/employees'), request, {
      context: employeesContext(),
    });
  }

  /** `PUT /employees/{id}` — `Employees.Edit`. */
  update(id: string, request: EmployeeWriteRequest): Observable<EmployeeResponse> {
    return this.http.put<EmployeeResponse>(joinApiUrl(this.baseUrl, `/employees/${id}`), request, {
      context: employeesContext(),
    });
  }

  /** `DELETE /employees/{id}` — soft-delete; `Employees.Edit`. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/employees/${id}`), {
      context: employeesContext(),
    });
  }
}
