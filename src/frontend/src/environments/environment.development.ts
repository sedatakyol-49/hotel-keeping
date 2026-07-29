import type { AppEnvironment } from './environment.model';

/**
 * Gelistirme ortami. Backend `http://localhost:5080` uzerinde calisir.
 * `proxy.conf.json` sayesinde `/api` istekleri dev-server tarafindan
 * backend'e yonlendirilir; bu nedenle CORS ayari gerekmez.
 */
export const environment: AppEnvironment = {
  production: false,
  apiBaseUrl: 'http://localhost:5080/api/v1',
  enableServiceWorker: false,
  logLevel: 'debug',
};
