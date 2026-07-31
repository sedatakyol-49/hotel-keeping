import type { AppEnvironment } from './environment.model';

/**
 * Gelistirme ortami. Backend varsayilan olarak `http://localhost:5080`.
 *
 * `apiBaseUrl` GORELIDIR ve istekleri `proxy.conf.json` tasir. Onceden mutlak
 * adres yaziliydi; o durumda vekil hic devreye girmiyordu ve panel yalnizca
 * backend'in CORS listesindeki tek origin'den (4200) calisiyordu — baska bir
 * portta `ng serve` yapan gelistirici, sebebi gorunmeyen bir "sunucuya
 * ulasilamiyor" hatasi aliyordu. Goreli adres hem dosyanin kendi aciklamasiyla
 * hem misafir uygulamasiyla ayni davranisi verir; hedef backend'i degistirmek
 * icin `--proxy-config` yeterlidir.
 */
export const environment: AppEnvironment = {
  production: false,
  apiBaseUrl: '/api/v1',
  enableServiceWorker: false,
  logLevel: 'debug',
};
