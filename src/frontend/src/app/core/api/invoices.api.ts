import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type {
  CancelInvoiceRequest,
  CreateInvoiceRequest,
  InvoiceDetailResponse,
  InvoiceListQuery,
  InvoiceResponse,
  RecordPaymentRequest,
  UpdateInvoiceRequest,
} from '../models/invoice.model';
import type { PagedResult } from '../models/paged-result.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

function invoicesContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/** Bos/null filtreler sorgu dizesine hic eklenmez. */
function toListParams(query: InvoiceListQuery): HttpParams {
  let params = new HttpParams()
    .set('page', String(query.page))
    .set('pageSize', String(query.pageSize));

  if (query.status) {
    params = params.set('status', query.status);
  }
  if (query.guestId) {
    params = params.set('guestId', query.guestId);
  }
  if (query.reservationId) {
    params = params.set('reservationId', query.reservationId);
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
 * `/api/v1/invoices` sozlesmesi (docs/api-contracts-invoices.md).
 *
 * **DELETE ucu bilincli olarak yoktur**: fatura silinmez, duzeltme yalnizca
 * iptal faturasiyla yapilir (GoBD §6.1/§6.4).
 */
@Injectable({ providedIn: 'root' })
export class InvoicesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /invoices` — sayfali + filtreli; `Invoices.View`. */
  list(query: InvoiceListQuery): Observable<PagedResult<InvoiceResponse>> {
    return this.http.get<PagedResult<InvoiceResponse>>(joinApiUrl(this.baseUrl, '/invoices'), {
      params: toListParams(query),
      context: invoicesContext(),
    });
  }

  /** `GET /invoices/{id}` — satirlar + odemeler + **denetim izi**. */
  getById(id: string): Observable<InvoiceDetailResponse> {
    return this.http.get<InvoiceDetailResponse>(joinApiUrl(this.baseUrl, `/invoices/${id}`), {
      context: invoicesContext(),
    });
  }

  /**
   * `POST /invoices` — **Draft** olusturur (numara YOK); `Invoices.Create`.
   * Iki yol birbirini disler: `reservationId` **veya** `lineItems`.
   */
  create(request: CreateInvoiceRequest): Observable<InvoiceDetailResponse> {
    return this.http.post<InvoiceDetailResponse>(
      joinApiUrl(this.baseUrl, '/invoices'),
      request,
      { context: invoicesContext() },
    );
  }

  /**
   * `PUT /invoices/{id}` — **yalnizca Draft**; satirlar tamamen degistirilir.
   * `Finalized`/`Paid`/`Cancelled` → 409 (ekran bu yolu hic gostermez).
   */
  update(id: string, request: UpdateInvoiceRequest): Observable<InvoiceDetailResponse> {
    return this.http.put<InvoiceDetailResponse>(
      joinApiUrl(this.baseUrl, `/invoices/${id}`),
      request,
      { context: invoicesContext() },
    );
  }

  /**
   * `POST /invoices/{id}/finalize` — `Invoices.Approve`.
   * Numara atanir (`{yil}-{6 hane}`), `issuedAt` damgalanir. **Geri alinamaz.**
   */
  finalize(id: string): Observable<InvoiceDetailResponse> {
    return this.http.post<InvoiceDetailResponse>(
      joinApiUrl(this.baseUrl, `/invoices/${id}/finalize`),
      {},
      { context: invoicesContext() },
    );
  }

  /**
   * `POST /invoices/{id}/cancel` — `Invoices.Cancel`; govde opsiyonel.
   * Taslak: dogrudan `Cancelled` (storno **uretilmez**).
   * Kesinlesmis/odenmis: orijinal korunur ve **yeni bir Stornorechnung** kesilir.
   */
  cancel(id: string, request: CancelInvoiceRequest = {}): Observable<InvoiceDetailResponse> {
    return this.http.post<InvoiceDetailResponse>(
      joinApiUrl(this.baseUrl, `/invoices/${id}/cancel`),
      request,
      { context: invoicesContext() },
    );
  }

  /**
   * `POST /invoices/{id}/payments` — `Invoices.Create`; 200 + detay yaniti
   * (odeme ayri adreslenebilir kaynak degildir, bu yuzden 201 degil).
   * **Fazla odeme → 409** (kurus toleransi yoktur).
   */
  recordPayment(id: string, request: RecordPaymentRequest): Observable<InvoiceDetailResponse> {
    return this.http.post<InvoiceDetailResponse>(
      joinApiUrl(this.baseUrl, `/invoices/${id}/payments`),
      request,
      { context: invoicesContext() },
    );
  }

  /**
   * `GET /invoices/{id}/pdf` → **501 Not Implemented** (bu fazda uretilmiyor).
   *
   * Bilincli olarak **cagrilmaz**: ekran indirme dugmesini devre disi gosterir
   * ve sahte indirme yapmaz. Metot sozlesmenin izini surdurmek icin burada
   * tutulur; DI'a kayitli bir `IInvoiceExporter` eklendiginde etkinlestirilir.
   */
  pdfUrl(id: string): string {
    return joinApiUrl(this.baseUrl, `/invoices/${id}/pdf`);
  }
}
