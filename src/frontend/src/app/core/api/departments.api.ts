import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type { DepartmentResponse, DepartmentWriteRequest } from '../models/employee.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/** Hatalar ekranin kendi hata blogunda gosterilir; global serit bastirilir. */
function departmentsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/**
 * `/api/v1/departments` sozlesmesi (docs/api-contracts.md — Personel).
 *
 * Departman sayisi az oldugu icin liste **duz dizi** doner, sayfalama yoktur.
 * Sozlesmede tek kayit okuma ucu (`GET /departments/{id}`) **yoktur**;
 * duzenleme ekrani kaydi liste yanitindan cozer.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /departments` — `Employees.View`; `employeeCount` ile birlikte. */
  list(): Observable<readonly DepartmentResponse[]> {
    return this.http.get<readonly DepartmentResponse[]>(joinApiUrl(this.baseUrl, '/departments'), {
      context: departmentsContext(),
    });
  }

  /** `POST /departments` — `Employees.Edit`; ad cakismasinda 409. */
  create(request: DepartmentWriteRequest): Observable<DepartmentResponse> {
    return this.http.post<DepartmentResponse>(joinApiUrl(this.baseUrl, '/departments'), request, {
      context: departmentsContext(),
    });
  }

  /** `PUT /departments/{id}` — `Employees.Edit`; ad cakismasinda 409. */
  update(id: string, request: DepartmentWriteRequest): Observable<DepartmentResponse> {
    return this.http.put<DepartmentResponse>(
      joinApiUrl(this.baseUrl, `/departments/${id}`),
      request,
      { context: departmentsContext() },
    );
  }

  /**
   * `DELETE /departments/{id}` — **hard delete** (departman soft-delete
   * edilmez); bagli calisan varsa backend 409 doner.
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/departments/${id}`), {
      context: departmentsContext(),
    });
  }
}
