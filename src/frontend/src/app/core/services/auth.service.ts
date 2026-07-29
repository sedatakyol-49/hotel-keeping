import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, Injector, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthApi } from '../api/auth.api';
import type { AuthenticatedUser, LoginRequest } from '../models/auth.model';
import { toApiError } from '../interceptors/problem-details.mapper';
import { AuthStore } from '../state/auth.store';
import { CurrentHotelService } from './current-hotel.service';
import { TokenStorageService } from './token-storage.service';

/**
 * Oturum akislarinin orkestrasyonu: HTTP (`AuthApi`) + token saklama
 * (`TokenStorageService`) + durum (`AuthStore`).
 *
 * Durum yalnizca store'da tutulur; bu servis kendi state'ini tasimaz.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly tokens = inject(TokenStorageService);
  private readonly store = inject(AuthStore);
  private readonly currentHotel = inject(CurrentHotelService);
  /**
   * `Router` bilincli olarak **tembel** cozulur: bu servis uygulama acilis
   * baslaticisinda (`provideAppInitializer`) kullanildigi icin, `Router` heniz
   * kurulurken enjekte edilmeye calisilirsa NG0200 (dairesel bagimlilik) olusur.
   */
  private readonly injector = inject(Injector);

  /**
   * `POST /auth/login`. Basarili olursa token'lar saklanir ve oturum kurulur.
   * Hata i18n anahtari olarak store'a yazilir; sablonda `translate` ile gosterilir.
   */
  async login(request: LoginRequest): Promise<boolean> {
    this.store.beginAuthentication();
    try {
      const response = await firstValueFrom(this.api.login(request));
      this.tokens.setTokens(response);
      this.store.setSession(response.user, this.currentHotel.readPreferredHotelId());
      return true;
    } catch (error: unknown) {
      this.tokens.clear();
      this.store.setAnonymous(this.toLoginErrorKey(error));
      return false;
    }
  }

  /**
   * Sayfa yenilendiginde oturumu sessizce geri yukler:
   * refresh token varsa `POST /auth/refresh` -> `GET /auth/me`.
   * Basarisizlikta kullanici anonim kabul edilir (hata gosterilmez).
   */
  async restoreSession(): Promise<boolean> {
    if (!this.tokens.hasRefreshToken()) {
      this.store.setAnonymous(null);
      return false;
    }
    this.store.beginAuthentication();
    try {
      const refreshToken = this.tokens.refreshToken();
      const tokens = await firstValueFrom(this.api.refresh({ refreshToken: refreshToken ?? '' }));
      this.tokens.setTokens(tokens);
      const user = await firstValueFrom(this.api.me());
      this.store.setSession(user, this.currentHotel.readPreferredHotelId());
      return true;
    } catch {
      this.tokens.clear();
      this.store.setAnonymous(null);
      return false;
    }
  }

  /** `GET /auth/me` — izinler/oteller degistiginde yeniden okunur. */
  async refreshCurrentUser(): Promise<AuthenticatedUser | null> {
    try {
      const user = await firstValueFrom(this.api.me());
      this.store.setSession(user, this.store.activeHotelId());
      return user;
    } catch {
      return null;
    }
  }

  /** Cikis: yerel durum ve token'lar temizlenir, login sayfasina yonlendirilir. */
  async logout(reasonKey: string | null = null): Promise<void> {
    this.tokens.clear();
    this.currentHotel.forget();
    this.store.setAnonymous(reasonKey);
    await this.injector.get(Router).navigate(['/login']);
  }

  private toLoginErrorKey(error: unknown): string {
    if (error instanceof HttpErrorResponse && (error.status === 400 || error.status === 401)) {
      return 'auth.invalidCredentials';
    }
    return toApiError(error).messageKey;
  }
}
