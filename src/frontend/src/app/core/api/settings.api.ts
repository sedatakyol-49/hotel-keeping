import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { SKIP_ERROR_NOTIFICATION } from '../interceptors/http-context.tokens';
import type {
  HeadOfficeSettingsResponse,
  HotelListItemResponse,
  HotelResponse,
  UpdateHeadOfficeSettingsRequest,
  UpdateHotelSettingsRequest,
} from '../models/settings.model';
import { API_BASE_URL, joinApiUrl } from './api-base';

/** Hatalar ekranin kendi hata blogunda/alan hatasinda gosterilir; global serit bastirilir. */
function settingsContext(): HttpContext {
  return new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);
}

/**
 * `/api/v1/hotels` ve `/api/v1/head-office` sozlesmesi
 * (docs/api-contracts.md — Hotels & Ayarlar).
 *
 * Bu uclar `X-Hotel-Id` basligina **bagli degildir**: hangi otelin okunacagi route'taki
 * kimlikten gelir. Erisilemeyen otel 404 doner (403 degil) — varligi sizdirilmaz.
 */
@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** `GET /hotels` — `Hotels.View`; kullanicinin erisebildigi oteller. */
  listHotels(): Observable<readonly HotelListItemResponse[]> {
    return this.http.get<readonly HotelListItemResponse[]>(joinApiUrl(this.baseUrl, '/hotels'), {
      context: settingsContext(),
    });
  }

  /** `GET /hotels/{id}` — `Hotels.View`. */
  getHotel(id: string): Observable<HotelResponse> {
    return this.http.get<HotelResponse>(joinApiUrl(this.baseUrl, `/hotels/${id}`), {
      context: settingsContext(),
    });
  }

  /** `PUT /hotels/{id}/settings` — `Settings.Manage`. */
  updateHotel(id: string, request: UpdateHotelSettingsRequest): Observable<HotelResponse> {
    return this.http.put<HotelResponse>(
      joinApiUrl(this.baseUrl, `/hotels/${id}/settings`),
      request,
      { context: settingsContext() },
    );
  }

  /** `GET /head-office/settings` — `Settings.Manage`. */
  getHeadOffice(): Observable<HeadOfficeSettingsResponse> {
    return this.http.get<HeadOfficeSettingsResponse>(
      joinApiUrl(this.baseUrl, '/head-office/settings'),
      { context: settingsContext() },
    );
  }

  /** `PUT /head-office/settings` — `Settings.Manage`; hangi Head Office oldugu kimlikten gelir. */
  updateHeadOffice(
    request: UpdateHeadOfficeSettingsRequest,
  ): Observable<HeadOfficeSettingsResponse> {
    return this.http.put<HeadOfficeSettingsResponse>(
      joinApiUrl(this.baseUrl, '/head-office/settings'),
      request,
      { context: settingsContext() },
    );
  }
}
