import { Injectable, computed, signal } from '@angular/core';

import type { AuthStatus, AuthenticatedUser } from '../models/auth.model';
import type { HotelSummary } from '../models/hotel.model';
import type { PermissionKey, PermissionMatchMode } from '../models/permission.model';

/**
 * Oturum durumunun tek kaynagi (signal store).
 *
 * Tasarim notu: bu store bilincli olarak **saf durum**tur — HTTP bagimliligi yoktur.
 * Ag cagrilarini `AuthService` yapar, sonucu buraya yazar. Bu sayede store birim
 * testlerinde HttpClient kurulumu olmadan dogrudan ornekleneblir ve interceptor'lar
 * store'a bagimli olurken dairesel bagimlilik olusmaz.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly _user = signal<AuthenticatedUser | null>(null);
  private readonly _status = signal<AuthStatus>('unknown');
  /** i18n anahtari olarak tutulur (ornek: `auth.invalidCredentials`). */
  private readonly _errorKey = signal<string | null>(null);
  private readonly _activeHotelId = signal<string | null>(null);

  readonly user = this._user.asReadonly();
  readonly status = this._status.asReadonly();
  readonly errorKey = this._errorKey.asReadonly();
  readonly activeHotelId = this._activeHotelId.asReadonly();

  readonly isAuthenticated = computed(() => this._user() !== null);
  readonly isBusy = computed(() => this._status() === 'authenticating');
  /** Oturum geri yukleme denemesi tamamlandi mi (router guard'lari icin). */
  readonly isResolved = computed(() => this._status() !== 'unknown');

  readonly permissions = computed<ReadonlySet<string>>(
    () => new Set<string>(this._user()?.permissions ?? []),
  );
  readonly roles = computed<readonly string[]>(() => this._user()?.roles ?? []);
  readonly hotels = computed<readonly HotelSummary[]>(() => this._user()?.hotels ?? []);
  readonly canAccessAllHotels = computed(() => this._user()?.canAccessAllHotels ?? false);

  /** Aktif otel nesnesi; `null` ise konsolide (tum oteller) gorunum. */
  readonly activeHotel = computed<HotelSummary | null>(() => {
    const id = this._activeHotelId();
    return id === null ? null : (this.hotels().find((hotel) => hotel.id === id) ?? null);
  });

  readonly displayName = computed(() => {
    const user = this._user();
    if (!user) {
      return '';
    }
    if (user.displayName) {
      return user.displayName;
    }
    const parts = [user.firstName, user.lastName].filter(Boolean) as string[];
    return parts.length > 0 ? parts.join(' ') : user.email;
  });

  /** Avatar yerine kullanilan tipografik bas harfler (ikon seti kullanilmaz). */
  readonly initials = computed(() => {
    const name = this.displayName();
    if (!name) {
      return '';
    }
    const words = name.split(/[\s@._-]+/).filter(Boolean);
    return words
      .slice(0, 2)
      .map((word) => word.charAt(0).toLocaleUpperCase())
      .join('');
  });

  hasPermission(key: PermissionKey): boolean {
    return this.permissions().has(key);
  }

  hasAnyPermission(keys: readonly PermissionKey[]): boolean {
    return keys.length === 0 || keys.some((key) => this.hasPermission(key));
  }

  hasAllPermissions(keys: readonly PermissionKey[]): boolean {
    return keys.every((key) => this.hasPermission(key));
  }

  matchesPermissions(keys: readonly PermissionKey[], mode: PermissionMatchMode = 'any'): boolean {
    if (keys.length === 0) {
      return true;
    }
    return mode === 'all' ? this.hasAllPermissions(keys) : this.hasAnyPermission(keys);
  }

  /** Kullanicinin belirtilen otele erisimi var mi (Head Office bypass dahil). */
  canAccessHotel(hotelId: string): boolean {
    return this.canAccessAllHotels() || this.hotels().some((hotel) => hotel.id === hotelId);
  }

  // --- Aksiyonlar ---------------------------------------------------------

  beginAuthentication(): void {
    this._status.set('authenticating');
    this._errorKey.set(null);
  }

  /**
   * Oturumu kurar. Aktif otel sirasiyla: istenen otel -> kullanicinin varsayilan
   * oteli -> erisilebilen ilk otel. Head Office kullanicisinda otel yoksa `null`
   * kalir (konsolide gorunum).
   */
  setSession(user: AuthenticatedUser, preferredHotelId?: string | null): void {
    this._user.set(user);
    this._status.set('authenticated');
    this._errorKey.set(null);

    const candidates = [preferredHotelId, user.defaultHotelId, user.hotels[0]?.id];
    const resolved = candidates.find(
      (id): id is string => typeof id === 'string' && user.hotels.some((h) => h.id === id),
    );
    this._activeHotelId.set(resolved ?? null);
  }

  /**
   * Aktif oteli degistirir. `null` = konsolide gorunum (yalnizca Head Office).
   * Yetkisiz otel talebinde durum degismez ve `false` doner.
   */
  setActiveHotel(hotelId: string | null): boolean {
    if (hotelId === null) {
      if (!this.canAccessAllHotels()) {
        return false;
      }
      this._activeHotelId.set(null);
      return true;
    }
    if (!this.hotels().some((hotel) => hotel.id === hotelId)) {
      return false;
    }
    this._activeHotelId.set(hotelId);
    return true;
  }

  setAnonymous(errorKey: string | null = null): void {
    this._user.set(null);
    this._activeHotelId.set(null);
    this._status.set('anonymous');
    this._errorKey.set(errorKey);
  }

  setError(errorKey: string | null): void {
    this._errorKey.set(errorKey);
    if (!this.isAuthenticated()) {
      this._status.set('anonymous');
    }
  }

  clearError(): void {
    this._errorKey.set(null);
  }

  /** Tam sifirlama — cikis ve 401 sonrasi kullanilir. */
  clear(): void {
    this.setAnonymous(null);
  }
}
