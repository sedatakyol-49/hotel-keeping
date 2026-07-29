import { HttpContextToken } from '@angular/common/http';

/** `true` ise `Authorization` basligi eklenmez (login/refresh cagrilari). */
export const SKIP_AUTH_HEADER = new HttpContextToken<boolean>(() => false);

/** `true` ise hata global bildirim seridine dusmez; cagiran kendisi gosterir. */
export const SKIP_ERROR_NOTIFICATION = new HttpContextToken<boolean>(() => false);

/** `true` ise `X-Hotel-Id` basligi eklenmez (otel bagimsiz uc noktalar). */
export const SKIP_HOTEL_CONTEXT = new HttpContextToken<boolean>(() => false);

/**
 * `true` ise 401 yaniti otomatik `/login` yonlendirmesi tetiklemez.
 * `/auth/*` cagrilarinda kullanilir: oturum akisini `AuthService` yonetir ve
 * acilis baslaticisi sirasinda router heniz hazir olmayabilir.
 */
export const SKIP_AUTH_REDIRECT = new HttpContextToken<boolean>(() => false);
