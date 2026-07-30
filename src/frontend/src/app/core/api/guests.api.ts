import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type { GuestListQuery, GuestResponse, GuestWriteRequest } from '../models/guest.model';
import type { PagedResult } from '../models/paged-result.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/**
 * Misafir uclarinda hatalar cagiran ekranda (liste hata blogu / form alan
 * hatasi / satir aksiyonu) gosterildigi icin global bildirim seridi bastirilir.
 */
function guestsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/**
 * `/api/v1/guests` sozlesmesi (docs/api-contracts-reservations.md → Guests).
 *
 * Izinler: okuma `Reservations.View`, yazma `Reservations.Create`
 * (ayri bir `Guests.*` anahtari bilincli olarak tanimlanmadi).
 */
@Injectable({ providedIn: 'root' })
export class GuestsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /guests` — sayfali + arama (ad/soyad/e-posta contains). */
  list(query: GuestListQuery): Observable<PagedResult<GuestResponse>> {
    let params = new HttpParams()
      .set('page', String(query.page))
      .set('pageSize', String(query.pageSize));
    const search = query.search?.trim();
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<PagedResult<GuestResponse>>(joinApiUrl(this.baseUrl, '/guests'), {
      params,
      context: guestsContext(),
    });
  }

  /** `GET /guests/{id}` — yanitta `stayCount` **dolu** gelir. */
  getById(id: string): Observable<GuestResponse> {
    return this.http.get<GuestResponse>(joinApiUrl(this.baseUrl, `/guests/${id}`), {
      context: guestsContext(),
    });
  }

  /** `POST /guests` — 201 + `Location`. Benzersizlik kurali **yoktur**. */
  create(request: GuestWriteRequest): Observable<GuestResponse> {
    return this.http.post<GuestResponse>(joinApiUrl(this.baseUrl, '/guests'), request, {
      context: guestsContext(),
    });
  }

  /** `PUT /guests/{id}` — tam guncelleme. */
  update(id: string, request: GuestWriteRequest): Observable<GuestResponse> {
    return this.http.put<GuestResponse>(joinApiUrl(this.baseUrl, `/guests/${id}`), request, {
      context: guestsContext(),
    });
  }

  /**
   * `DELETE /guests/{id}` — soft-delete.
   * Aktif (`CheckedIn`) veya gelecek tarihli rezervasyonu varsa **409**.
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(joinApiUrl(this.baseUrl, `/guests/${id}`), {
      context: guestsContext(),
    });
  }
}
