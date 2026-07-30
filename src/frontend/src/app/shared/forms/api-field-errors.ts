import type { AbstractControl, FormGroup } from '@angular/forms';

import type { ApiError } from '../../core/models/problem-details.model';

/** Sunucudan gelen alan hatalarinin tutuldugu kontrol hatasi anahtari. */
export const SERVER_ERROR_KEY = 'server';

function camelize(segment: string): string {
  return segment.length > 0 ? segment.charAt(0).toLowerCase() + segment.slice(1) : segment;
}

/**
 * `errors` sozlugundeki alan adini form kontrolune cozer.
 * Backend PascalCase kullanir (`"Number"`, `"Translations.de.Name"`), formda
 * kontroller camelCase'tir; her iki yazim da denenir.
 */
function findControl(form: FormGroup, field: string): AbstractControl | null {
  const path = field.split('.').filter(Boolean);
  if (path.length === 0) {
    return null;
  }
  return form.get(path) ?? form.get(path.map(camelize)) ?? null;
}

/**
 * 400 yanitindaki `errors` sozlugunu ilgili form alanlarina baglar.
 * Eslesmeyen mesajlar (ornek: alan adi olmayan genel hatalar) form seviyesinde
 * gosterilmek uzere geri dondurulur.
 *
 * Not: kullanici alani yeniden duzenledigi anda Angular validator'lari calisir
 * ve `server` hatasi kendiliginden temizlenir.
 */
export function applyApiFieldErrors(form: FormGroup, error: ApiError): readonly string[] {
  const unmatched: string[] = [];

  for (const [field, messages] of Object.entries(error.fieldErrors ?? {})) {
    const control = findControl(form, field);
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), [SERVER_ERROR_KEY]: messages });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }

  return unmatched;
}

/** Bir kontrole bagli sunucu hata mesajlari (i18n'e girmez — backend cevirir). */
export function serverErrorMessages(control: AbstractControl | null): readonly string[] {
  const value = control?.errors?.[SERVER_ERROR_KEY];
  return Array.isArray(value) ? (value as readonly string[]) : [];
}

/** Alanla eslesmeyen sunucu mesajlarini bir kontrol uzerine elle isler. */
export function setServerError(control: AbstractControl | null, messageKey: string): void {
  if (!control) {
    return;
  }
  control.setErrors({ ...(control.errors ?? {}), conflict: messageKey });
  control.markAsTouched();
}
