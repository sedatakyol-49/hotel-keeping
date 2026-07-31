import type { GuestEnvironment } from './environment.model';

/** Production ortami (reverse proxy arkasinda, ayni origin uzerinden API). */
export const environment: GuestEnvironment = {
  production: true,
  siteOrigin: 'https://www.hotelcore.example',
  apiBaseUrl: '/api/v1',
  /* Dagitimda otel basina alan adi -> slug (bkz. environment.model.ts). */
  hotelSlug: 'berlin-mitte',
};
