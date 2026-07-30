import { Pipe, inject, type PipeTransform } from '@angular/core';

import { LanguageStore } from '@hotelcore/shared';

/**
 * Para birimi **olmayan** sayi bicimlendirme — locale'e gore binlik ayraci
 * (`de-DE` -> `36.600`, `1.234,56`).
 *
 * Raporlarda oda-gece sayilari (tam sayi) ve yuzdeler (iki ondalik) icin
 * kullanilir; para tutarlari `hcMoney` ile gosterilir.
 */
@Pipe({ name: 'hcNum' })
export class NumberPipe implements PipeTransform {
  private readonly languageStore = inject(LanguageStore);

  transform(value: number | null | undefined, fractionDigits = 2): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    return new Intl.NumberFormat(this.languageStore.locale(), {
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits,
    }).format(value);
  }
}
