import { Pipe, inject, type PipeTransform } from '@angular/core';

import { LanguageStore } from '@hotelcore/shared';

/**
 * Para bicimlendirme — locale'e gore (`de-DE` -> `1.234,56 €`).
 * Para birimi otel bazinda degistigi icin cagrida acikca verilir.
 *
 * **`currency = null`**: tutar **sembolsuz** bicimlenir. Bu, konsolide
 * raporlarda kapsamdaki oteller farkli para birimleri kullandiginda
 * (`scope.hasMixedCurrencies`) gereklidir — toplam farkli birimlerin aritmetik
 * toplamidir ve yanlis bir sembolle etiketlenmesi sayiyi yalan haline getirir.
 * Sayi gizlenmez; cagiran ekran uyari metnini ayrica gosterir.
 */
@Pipe({ name: 'hcMoney' })
export class MoneyPipe implements PipeTransform {
  private readonly languageStore = inject(LanguageStore);

  transform(value: number | null | undefined, currency: string | null = 'EUR'): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    const options: Intl.NumberFormatOptions =
      currency === null ? {} : { style: 'currency', currency };

    return new Intl.NumberFormat(this.languageStore.locale(), {
      ...options,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }
}
