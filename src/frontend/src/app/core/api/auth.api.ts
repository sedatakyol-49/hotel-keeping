import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import type {
  CurrentUserResponse,
  LoginRequest,
  LoginResponse,
  RefreshTokenRequest,
  RefreshTokenResponse,
} from '../models/auth.model';
import {
  SKIP_AUTH_HEADER,
  SKIP_AUTH_REDIRECT,
  SKIP_ERROR_NOTIFICATION,
} from '../interceptors/http-context.tokens';
import { API_BASE_URL, joinApiUrl } from './api-base';

/** `/auth/*` cagrilari icin ortak HttpContext: global hata/yonlendirme kapali. */
function authContext(anonymous: boolean): HttpContext {
  return new HttpContext()
    .set(SKIP_AUTH_HEADER, anonymous)
    .set(SKIP_ERROR_NOTIFICATION, true)
    .set(SKIP_AUTH_REDIRECT, true);
}

/**
 * `/api/v1/auth/*` sozlesmesi (docs/api-contracts.md — Auth).
 * Header yonetimi interceptor'lara aittir; burada yalnizca istisna
 * durumlar `HttpContext` ile isaretlenir.
 */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /**
   * `POST /auth/login` — anonim.
   * Hata gosterimi login sayfasinda yapildigi icin global bildirim bastirilir.
   */
  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(joinApiUrl(this.baseUrl, '/auth/login'), request, {
      context: authContext(true),
    });
  }

  /** `POST /auth/refresh` — anonim; suresi dolan access token'i yeniler. */
  refresh(request: RefreshTokenRequest): Observable<RefreshTokenResponse> {
    return this.http.post<RefreshTokenResponse>(
      joinApiUrl(this.baseUrl, '/auth/refresh'),
      request,
      { context: authContext(true) },
    );
  }

  /** `GET /auth/me` — aktif kullanici, izinleri ve erisilebilir otelleri. */
  me(): Observable<CurrentUserResponse> {
    return this.http.get<CurrentUserResponse>(joinApiUrl(this.baseUrl, '/auth/me'), {
      context: authContext(false),
    });
  }
}
