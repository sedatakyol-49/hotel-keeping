import { HttpClient, HttpParams } from '@angular/common/http';
import { InjectionToken, Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';

import { environment } from '../../../environments/environment';
import { toPublicError } from './public-error';
import type {
  PublicAvailabilityQuery,
  PublicAvailabilityResponse,
  PublicBookingLookupRequest,
  PublicBookingResponse,
  PublicCancelBookingRequest,
  PublicCreateBookingRequest,
  PublicCreateHoldRequest,
  PublicHold,
  PublicHotel,
  PublicLegalResponse,
  PublicRoomTypeDetail,
  PublicRoomTypeSummary,
} from './public-models';

/**
 * API taban adresi (`/api/v1`). Token uzerinden verilir ki testler gercek bir
 * adres kurmadan calissin (panelin `API_BASE_URL` deseniyle ayni).
 */
export const API_BASE_URL = new InjectionToken<string>('GUEST_API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl,
});

/**
 * AKTIF OTEL — sozlesmede yol parametresi (`/public/hotels/{hotelSlug}/...`).
 *
 * NEDEN TOKEN, NEDEN ROTA SEGMENTI DEGIL (sapma, raporlanmistir):
 * Mimari §4.1 otelin **URL yolundan** belirlenmesini sart kosar ve bunu
 * "API yine slug alir" diye netlestirir; ayni bolum, `Hotel.PublicHost`
 * doluysa **host -> slug** cevirisinin edge/SSR katmaninda yapilmasini acik
 * bir katman olarak birakir. Bu tur tek otelli (otel basina alan adi) dagitimi
 * hedefliyor; slug bu yuzden yapilandirmadan gelir ve **her API cagrisinda
 * yolda** durur. Cok otelli marka sitesi eklenecegi gun tek degisiklik bu
 * token'in bir rota `resolve`'undan beslenmesidir; API sozlesmesi degismez.
 */
export const GUEST_HOTEL_SLUG = new InjectionToken<string>('GUEST_HOTEL_SLUG', {
  providedIn: 'root',
  factory: () => environment.hotelSlug,
});

/**
 * Public uclarin tip-guvenli istemcisi (sozlesme §1.1 — 13 uc).
 *
 * Kurallar:
 *  - Hicbir uca `Authorization` gonderilmez; public yuzey anonimdir.
 *  - Her hata `PublicApiError`'a cevrilir; ekranlar `HttpErrorResponse`
 *    gormez, dolayisiyla ham hata kodu/metni ekrana sizamaz.
 *  - Musaitlik/hold/booking uclari onbelleklenmez (sunucu `no-store` doner);
 *    istemci tarafinda da tekrar cagrilmalari ucuzdur.
 */
@Injectable({ providedIn: 'root' })
export class PublicBookingApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL).replace(/\/+$/, '');
  private readonly slug = inject(GUEST_HOTEL_SLUG);

  /** `/api/v1/public/hotels/{slug}` */
  private get hotelUrl(): string {
    return `${this.baseUrl}/public/hotels/${encodeURIComponent(this.slug)}`;
  }

  /** 2 — otel kunyesi ve politikalari. */
  getHotel(): Observable<PublicHotel> {
    return this.get<PublicHotel>(this.hotelUrl);
  }

  /** 3 — Impressum / Datenschutz / AGB. */
  getLegal(): Observable<PublicLegalResponse> {
    return this.get<PublicLegalResponse>(`${this.hotelUrl}/legal`);
  }

  /** 4 — katalog. */
  getRoomTypes(): Observable<readonly PublicRoomTypeSummary[]> {
    return this.get<readonly PublicRoomTypeSummary[]>(`${this.hotelUrl}/room-types`);
  }

  /** 5 — oda tipi detayi (`roomTypeCode` buyuk/kucuk harf duyarsiz). */
  getRoomType(code: string): Observable<PublicRoomTypeDetail> {
    return this.get<PublicRoomTypeDetail>(
      `${this.hotelUrl}/room-types/${encodeURIComponent(code)}`,
    );
  }

  /** 6 — musaitlik + fiyat teklifi. Hold OLUSTURMAZ. */
  getAvailability(query: PublicAvailabilityQuery): Observable<PublicAvailabilityResponse> {
    const params = new HttpParams()
      .set('checkIn', query.checkIn)
      .set('checkOut', query.checkOut)
      .set('adults', String(query.adults))
      .set('children', String(query.children));

    return this.http
      .get<PublicAvailabilityResponse>(`${this.hotelUrl}/availability`, { params })
      .pipe(catchError(fail));
  }

  /** 7 — teklifi 15 dakika dondurur. */
  createHold(request: PublicCreateHoldRequest): Observable<PublicHold> {
    return this.post<PublicHold>(`${this.hotelUrl}/holds`, request);
  }

  /** 8 — kalan sure + donmus teklif (sayfa yenilendiginde). */
  getHold(token: string): Observable<PublicHold> {
    return this.get<PublicHold>(`${this.hotelUrl}/holds/${encodeURIComponent(token)}`);
  }

  /** 9 — envanteri hemen birak (idempotent, her zaman 204). */
  releaseHold(token: string): Observable<void> {
    return this.http
      .delete<void>(`${this.hotelUrl}/holds/${encodeURIComponent(token)}`)
      .pipe(catchError(fail));
  }

  /** 10 — rezervasyon olustur. */
  createBooking(request: PublicCreateBookingRequest): Observable<PublicBookingResponse> {
    return this.post<PublicBookingResponse>(`${this.hotelUrl}/bookings`, request);
  }

  /** 11 — sorgulama (`accessToken` tasiyici kimlik bilgisidir). */
  getBooking(accessToken: string): Observable<PublicBookingResponse> {
    return this.get<PublicBookingResponse>(
      `${this.hotelUrl}/bookings/${encodeURIComponent(accessToken)}`,
    );
  }

  /** 12 — iptal. */
  cancelBooking(
    accessToken: string,
    request: PublicCancelBookingRequest,
  ): Observable<PublicBookingResponse> {
    return this.post<PublicBookingResponse>(
      `${this.hotelUrl}/bookings/${encodeURIComponent(accessToken)}/cancel`,
      request,
    );
  }

  /**
   * 13 — baglantiyi e-postayla yeniden gonder.
   * **Hicbir kosulda veri dondurmez**; eslesme olsa da olmasa da 202.
   */
  lookupBooking(request: PublicBookingLookupRequest): Observable<void> {
    return this.post<void>(`${this.hotelUrl}/bookings/lookup`, request);
  }

  private get<T>(url: string): Observable<T> {
    return this.http.get<T>(url).pipe(catchError(fail));
  }

  private post<T>(url: string, body: unknown): Observable<T> {
    return this.http.post<T>(url, body).pipe(catchError(fail));
  }
}

function fail(error: unknown): Observable<never> {
  return throwError(() => toPublicError(error));
}
