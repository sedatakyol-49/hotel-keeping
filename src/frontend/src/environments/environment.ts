import type { AppEnvironment } from './environment.model';

/**
 * Production ortami. `ng build` (production) sirasinda kullanilir.
 * API ayni origin uzerinden servis edilir (reverse proxy arkasinda).
 */
export const environment: AppEnvironment = {
  production: true,
  apiBaseUrl: '/api/v1',
  enableServiceWorker: true,
  logLevel: 'error',
};
