import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type {
  OccupancyReportResponse,
  ReportRangeQuery,
  RevenueReportResponse,
} from '../models/report.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/**
 * Rapor hatalari **ilgili bolumun icinde** gosterilir (biri hata verse de
 * digeri render edilmeye devam eder), bu yuzden global bildirim seridi
 * bastirilir.
 */
function reportsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/** Kapali aralik: `from` ve `to` **her iki uc da dahildir**. */
function toRangeParams(query: ReportRangeQuery): HttpParams {
  return new HttpParams().set('from', query.from).set('to', query.to);
}

/**
 * `/api/v1/reports` sozlesmesi (docs/api-contracts-reports.md).
 *
 * Her iki uc da `Reports.View` ister ve **aktif otel ZORUNLU DEGILDIR**:
 * `X-Hotel-Id` gonderilmezse (Head Office kullanicisi otel secmemisse) rapor
 * konsolide hesaplanir ve yanittaki `scope` bunu soyler. Header yonetimi
 * interceptor'lara aittir.
 */
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /reports/occupancy?from=&to=` — oda-gece, kapasite, doluluk + gunluk seri. */
  occupancy(query: ReportRangeQuery): Observable<OccupancyReportResponse> {
    return this.http.get<OccupancyReportResponse>(joinApiUrl(this.baseUrl, '/reports/occupancy'), {
      params: toRangeParams(query),
      context: reportsContext(),
    });
  }

  /** `GET /reports/revenue?from=&to=` — ciro, ADR, RevPAR, kanal dagilimi + gunluk seri. */
  revenue(query: ReportRangeQuery): Observable<RevenueReportResponse> {
    return this.http.get<RevenueReportResponse>(joinApiUrl(this.baseUrl, '/reports/revenue'), {
      params: toRangeParams(query),
      context: reportsContext(),
    });
  }
}
