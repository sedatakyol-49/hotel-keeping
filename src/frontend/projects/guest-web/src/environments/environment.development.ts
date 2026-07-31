import type { GuestEnvironment } from './environment.model';

/**
 * Gelistirme ortami: `ng serve` (SSR) 4300 portunda calisir.
 *
 * `apiBaseUrl` GORELIDIR: istekler dev-server'in `/api` proxy'sinden gecer
 * (`proxy.conf.mjs`). Boylece hem tarayici hem SSR ayni adresi kullanir ve
 * hedef backend `GUEST_API_TARGET` ortam degiskeniyle degistirilebilir —
 * uc henuz canli degilken bir mock'a bakmak icin kod degistirmek gerekmez.
 */
export const environment: GuestEnvironment = {
  production: false,
  siteOrigin: 'http://localhost:4300',
  apiBaseUrl: '/api/v1',
  hotelSlug: 'berlin-mitte',
};
