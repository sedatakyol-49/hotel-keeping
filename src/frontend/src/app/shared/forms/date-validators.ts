import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Tarih alanlari `<input type="date">` ile tutulur; deger her zaman ISO
 * `YYYY-MM-DD` bicimindedir (saat/zaman dilimi yoktur). Backend de bu bicimi
 * bekler (`hiredOn`, `terminatedOn`), bu yuzden donusum yapilmaz — yalnizca
 * dogrulama gerekir.
 */

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

function isBlank(value: unknown): boolean {
  return value === null || value === undefined || String(value).trim() === '';
}

/** Deger gecerli bir ISO tarihi mi (`2024-03-01`)? */
export function isIsoDate(value: string | null | undefined): boolean {
  if (value === null || value === undefined || !ISO_DATE.test(value)) {
    return false;
  }
  const date = new Date(`${value}T00:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

/** Bos degeri kabul eder (zorunluluk `Validators.required` isi). */
export function isoDateValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (isBlank(control.value)) {
      return null;
    }
    return isIsoDate(String(control.value)) ? null : { dateFormat: true };
  };
}

/**
 * Grup seviyesinde sira dogrulamasi: `end >= start` (ornek: `terminatedOn`
 * >= `hiredOn`). Hata bilincli olarak **yalnizca gruba** islenir; alt
 * kontrollerin hatalari degistirilmez (dogrulama sirasinda kontrol mutasyonu
 * yapmamak icin). Mesaji alanin yaninda gostermek isteyen ekran
 * `form.errors?.[errorKey]` degerini okur.
 *
 * Taraflardan biri bos veya bicimsiz ise sira dogrulanmaz; o durum
 * `Validators.required` / `isoDateValidator` isidir. ISO tarihleri sozluksel
 * olarak siralanabilir, bu yuzden `Date` nesnesine cevrilmeden karsilastirilir.
 */
export function dateOrderValidator(
  startControlName: string,
  endControlName: string,
  errorKey = 'dateOrder',
): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = group.get(startControlName);
    const end = group.get(endControlName);
    if (!start || !end) {
      return null;
    }

    const startValue = isBlank(start.value) ? null : String(start.value);
    const endValue = isBlank(end.value) ? null : String(end.value);
    if (
      startValue === null ||
      endValue === null ||
      !isIsoDate(startValue) ||
      !isIsoDate(endValue)
    ) {
      return null;
    }

    return endValue >= startValue ? null : { [errorKey]: true };
  };
}
