import { Injectable, signal } from '@angular/core';

import type { AuthTokens } from '../models/auth.model';

const REFRESH_TOKEN_KEY = 'hotelcore.refreshToken';

/**
 * Token saklama stratejisi
 * ------------------------
 * 1. **Access token yalnizca bellekte** (`signal`) tutulur. localStorage'a
 *    yazilmaz; boylece XSS ile calinabilecek pencere sekme omru ile sinirlanir
 *    ve sekme kapaninca token diskte kalmaz.
 * 2. **Refresh token localStorage'da** tutulur; yalnizca bu sayede sayfa
 *    yenilendiginde oturum `POST /auth/refresh` ile sessizce geri yuklenebilir.
 *    Kabul edilen odun: refresh token XSS'e aciktir, bu yuzden backend'in
 *    tek kullanimlik (rotating) refresh token uretmesi ve sunucu tarafinda
 *    iptal edilebilir olmasi beklenir.
 * 3. **Hedef mimari:** backend `HttpOnly; Secure; SameSite=Strict` cookie
 *    destegi verdiginde refresh token cookie'ye tasinacak ve bu servisten
 *    localStorage kullanimi tamamen kaldirilacaktir. Erisim tek noktada
 *    toplandigi icin bu degisiklik yalnizca bu dosyayi etkiler.
 * 4. Sunucu tarafi render (SSR) yoktur; yine de `localStorage` erisimi
 *    savunmaci sekilde try/catch ile sarilir (private mode / kisitli tarayici).
 */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private readonly _accessToken = signal<string | null>(null);
  private readonly _refreshToken = signal<string | null>(readPersistedRefreshToken());

  readonly accessToken = this._accessToken.asReadonly();
  readonly refreshToken = this._refreshToken.asReadonly();

  hasRefreshToken(): boolean {
    return this._refreshToken() !== null;
  }

  setTokens(tokens: AuthTokens): void {
    this._accessToken.set(tokens.accessToken);
    this._refreshToken.set(tokens.refreshToken);
    persistRefreshToken(tokens.refreshToken);
  }

  clear(): void {
    this._accessToken.set(null);
    this._refreshToken.set(null);
    persistRefreshToken(null);
  }
}

function readPersistedRefreshToken(): string | null {
  try {
    return globalThis.localStorage?.getItem(REFRESH_TOKEN_KEY) ?? null;
  } catch {
    return null;
  }
}

function persistRefreshToken(token: string | null): void {
  try {
    if (token === null) {
      globalThis.localStorage?.removeItem(REFRESH_TOKEN_KEY);
    } else {
      globalThis.localStorage?.setItem(REFRESH_TOKEN_KEY, token);
    }
  } catch {
    // Depolama kullanilamiyorsa oturum yalnizca bellekte yasar — sessizce gec.
  }
}
