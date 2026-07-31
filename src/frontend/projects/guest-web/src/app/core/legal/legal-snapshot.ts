import { InjectionToken } from '@angular/core';

import type { PublicLegalResponse } from '../api/public-models';

/**
 * ===========================================================================
 * DERLEME ANI HUKUKI ICERIK (§5 DDG)
 * ===========================================================================
 *
 * Hukuki sayfalar prerender edilir (bkz. app.routes.server.ts). Prerender
 * derleme aninda calisir: gelen bir HTTP istegi YOKTUR, dolayisiyla
 * `apiUrlInterceptor` goreli adresi mutlaklastiramaz ve `GET /legal` istegi
 * duser. Sonuc, uretilen HTML'de bos bir Impressum'du — JavaScript
 * calistirmayan bir ziyaretci kunyeyi hic gormuyordu. §5 DDG kunyenin
 * "unmittelbar erreichbar" olmasini ister; bu kosul saglanmiyordu.
 *
 * Cozum: derleme oncesinde alinmis bir anlik goruntu (`npm run legal:snapshot`)
 * yalnizca **sunucu** yapilandirmasinda saglanir ve prerender sirasinda
 * `GET /legal` istegini karsilar. Uretilen HTML gercek metni tasir; Angular'in
 * hidrasyon aktarim onbellegi ayni yaniti istemciye tasidigi icin sayfa
 * hidrasyondan sonra bos gorunup yeniden dolmaz.
 *
 * Anlik goruntu **yalnizca prerender'da** devrededir: gercek bir istek varsa
 * (SSR) veya tarayicida canli API kullanilir, yani metin guncelligini kaybetmez.
 *
 * Tarayici paketi bu dosyanin ICERIGINI tasimaz: varsayilan deger `null`'dur,
 * JSON'u yalnizca sunucu yapilandirmasi (`legal-snapshot.server.ts`) import eder.
 */
export interface LegalSnapshot {
  readonly hotelSlug: string;
  readonly generatedAt: string;
  readonly documents: Readonly<Record<string, PublicLegalResponse>>;
}

export const LEGAL_SNAPSHOT = new InjectionToken<LegalSnapshot | null>('GUEST_LEGAL_SNAPSHOT', {
  providedIn: 'root',
  factory: () => null,
});

/** Istenen dil; yoksa anlik goruntudeki ilk dil (otelin varsayilanı). */
export function legalFor(
  snapshot: LegalSnapshot | null,
  culture: string,
): PublicLegalResponse | null {
  if (snapshot === null) {
    return null;
  }
  const exact = snapshot.documents[culture];
  if (exact !== undefined) {
    return exact;
  }
  const first = Object.values(snapshot.documents)[0];
  return first ?? null;
}
