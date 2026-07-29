import type { HotelSummary } from './hotel.model';
import type { PermissionKey } from './permission.model';

/**
 * Oturum acmis kullanici. `/auth/login` yanitindaki `user` alani ve
 * `/auth/me` yaniti ayni sekli paylasir (api-contracts.md — Auth).
 */
export interface AuthenticatedUser {
  readonly id: string;
  readonly email: string;
  readonly firstName?: string | null;
  readonly lastName?: string | null;
  /** Backend hazir bir gorunen ad dondurursa kullanilir. */
  readonly displayName?: string | null;
  /** JWT `culture` claim'i (`de` | `en` | `tr`). */
  readonly culture?: string | null;
  /** JWT `headOfficeId` claim'i. */
  readonly headOfficeId?: string | null;
  readonly roles: readonly string[];
  /** JWT `perm` claim'leri — mimari §7 izin anahtarlari. */
  readonly permissions: readonly PermissionKey[];
  /** JWT `hotel` claim'lerine karsilik gelen otel listesi. */
  readonly hotels: readonly HotelSummary[];
  /** JWT `allHotels` claim'i — Head Office bypass. */
  readonly canAccessAllHotels: boolean;
  /** Oturum acildiginda secilecek varsayilan otel. */
  readonly defaultHotelId?: string | null;
}

export interface LoginRequest {
  readonly email: string;
  readonly password: string;
}

export interface AuthTokens {
  readonly accessToken: string;
  readonly refreshToken: string;
  /** ISO-8601 UTC; yoksa token suresi JWT payload'indan okunur. */
  readonly expiresAtUtc?: string | null;
  readonly tokenType?: string | null;
}

/** `POST /api/v1/auth/login` yaniti. */
export interface LoginResponse extends AuthTokens {
  readonly user: AuthenticatedUser;
}

/** `POST /api/v1/auth/refresh` istegi ve yaniti. */
export interface RefreshTokenRequest {
  readonly refreshToken: string;
}

export type RefreshTokenResponse = AuthTokens;

/** `GET /api/v1/auth/me` yaniti. */
export type CurrentUserResponse = AuthenticatedUser;

/** Oturum durumu — guard'lar ve shell bu degeri okur. */
export type AuthStatus = 'unknown' | 'authenticating' | 'authenticated' | 'anonymous';
