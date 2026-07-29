/** Ortam yapilandirmasinin tip sozlesmesi. */
export interface AppEnvironment {
  readonly production: boolean;
  /** `/api/v1` ile biten mutlak veya goreli taban adres. */
  readonly apiBaseUrl: string;
  readonly enableServiceWorker: boolean;
  readonly logLevel: 'debug' | 'info' | 'warn' | 'error';
}
