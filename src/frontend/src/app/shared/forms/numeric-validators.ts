import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Sayisal alanlar metin kontrolu olarak tutulur: `<input type="number">` bos
 * birakildiginda `null` uretir ve `de-DE` kullanicilari ondalik ayirici olarak
 * virgul yazar. Donusum tek noktada, burada yapilir.
 */

/**
 * `"1.234,56"` / `"1234.56"` -> `1234.56`; cozumlenemezse `null`.
 * Virgul varsa Almanca yazim kabul edilir (nokta = binlik ayirici).
 *
 * Not: `<input type="number">` bagli bir kontrolde Angular'in
 * `NumberValueAccessor`'i modele **sayi** yazar (metin degil); bu yuzden sayi
 * girdisi de kabul edilir.
 */
export function parseDecimal(value: string | number | null | undefined): number | null {
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }
  const raw = value.trim().replace(/\s/g, '');
  if (raw === '') {
    return null;
  }
  const normalized = raw.includes(',') ? raw.replace(/\./g, '').replace(',', '.') : raw;
  if (!/^-?\d+(\.\d+)?$/.test(normalized)) {
    return null;
  }
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
}

/** `"-3"` -> `-3`; tam sayi degilse `null` (sayi girdisi de kabul edilir). */
export function parseInteger(value: string | number | null | undefined): number | null {
  if (value === null || value === undefined) {
    return null;
  }
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) ? value : null;
  }
  const normalized = value.trim();
  if (!/^-?\d+$/.test(normalized)) {
    return null;
  }
  const parsed = Number(normalized);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

function isBlank(value: unknown): boolean {
  return value === null || value === undefined || String(value).trim() === '';
}

/** Tam sayi + kapali aralik dogrulamasi (`floor`: −5…99 gibi). */
export function integerRangeValidator(min: number, max: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (isBlank(control.value)) {
      return null;
    }
    const parsed = parseInteger(String(control.value));
    if (parsed === null) {
      return { integerFormat: true };
    }
    return parsed < min || parsed > max ? { integerRange: { min, max } } : null;
  };
}

interface DecimalRangeOptions {
  readonly min?: number;
  readonly max?: number;
  /** `true` ise `min` degeri **dahil edilmez** (`sizeSqm > 0`). */
  readonly exclusiveMin?: boolean;
}

/** Ondalik sayi + aralik dogrulamasi (`basePrice >= 0`, `sizeSqm > 0`). */
export function decimalRangeValidator(options: DecimalRangeOptions): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (isBlank(control.value)) {
      return null;
    }
    const parsed = parseDecimal(String(control.value));
    if (parsed === null) {
      return { decimalFormat: true };
    }
    const { min, max, exclusiveMin } = options;
    if (min !== undefined && (exclusiveMin ? parsed <= min : parsed < min)) {
      return { decimalRange: { min, max, exclusiveMin } };
    }
    if (max !== undefined && parsed > max) {
      return { decimalRange: { min, max, exclusiveMin } };
    }
    return null;
  };
}
