import { InjectionToken } from '@angular/core';

import { environment } from '../../../environments/environment';

/**
 * API taban adresi (`/api/v1`). Token uzerinden verilir ki testlerde
 * kolayca degistirilebilsin.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => environment.apiBaseUrl,
});

/** `/auth/login` gibi goreli yollari taban adresle birlestirir. */
export function joinApiUrl(baseUrl: string, path: string): string {
  const normalizedBase = baseUrl.replace(/\/+$/, '');
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${normalizedBase}${normalizedPath}`;
}
