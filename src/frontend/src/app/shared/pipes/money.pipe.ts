import { Pipe, inject, type PipeTransform } from '@angular/core';

import { LanguageStore } from '../../core/state/language.store';

/**
 * Para bicimlendirme — locale'e gore (`de-DE` -> `1.234,56 €`).
 * Para birimi otel bazinda degistigi icin cagrida acikca verilir.
 */
@Pipe({ name: 'hcMoney' })
export class MoneyPipe implements PipeTransform {
  private readonly languageStore = inject(LanguageStore);

  transform(value: number | null | undefined, currency = 'EUR'): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    return new Intl.NumberFormat(this.languageStore.locale(), {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }
}
