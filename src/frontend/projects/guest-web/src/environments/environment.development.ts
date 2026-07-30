import type { GuestEnvironment } from './environment.model';

/** Gelistirme ortami: `ng serve` (SSR) 4300 portunda calisir. */
export const environment: GuestEnvironment = {
  production: false,
  siteOrigin: 'http://localhost:4300',
  apiBaseUrl: 'http://localhost:5080/api/v1',
};
