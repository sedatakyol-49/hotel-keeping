import type { Provider } from '@angular/core';

import snapshot from '../../../generated/legal-snapshot.json';
import { LEGAL_SNAPSHOT, type LegalSnapshot } from './legal-snapshot';

/**
 * Anlik goruntuyu **yalnizca sunucu** yapilandirmasina baglar (bkz. legal-snapshot.ts).
 * JSON burada import edildigi icin tarayici paketine girmez.
 *
 * Dosya uretilmis bir yapaydir (`npm run legal:snapshot`); bicimi
 * `scripts/legal-snapshot.mjs` icinde tanimlidir ve `--check` ile dogrulanir.
 */
export function provideLegalSnapshot(): Provider {
  return { provide: LEGAL_SNAPSHOT, useValue: snapshot as unknown as LegalSnapshot };
}
