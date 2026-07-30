import { Pipe, inject, type PipeTransform } from '@angular/core';

import { LanguageStore } from '@hotelcore/shared';

export type LocalizedDateStyle = 'short' | 'medium' | 'long' | 'dayMonth' | 'time' | 'weekdayShort';

/**
 * Aktif dile gore tarih bicimlendirme (`de-DE`, `en-GB`, `tr-TR`).
 * `LanguageStore` bir signal oldugu icin dil degisince sablon otomatik tazelenir.
 */
@Pipe({ name: 'hcDate' })
export class LocalizedDatePipe implements PipeTransform {
  private readonly languageStore = inject(LanguageStore);

  transform(
    value: Date | string | number | null | undefined,
    style: LocalizedDateStyle = 'medium',
  ): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }
    return new Intl.DateTimeFormat(this.languageStore.locale(), OPTIONS[style]).format(date);
  }
}

const OPTIONS: Readonly<Record<LocalizedDateStyle, Intl.DateTimeFormatOptions>> = {
  short: { day: '2-digit', month: '2-digit', year: '2-digit' },
  medium: { day: '2-digit', month: '2-digit', year: 'numeric' },
  long: { day: 'numeric', month: 'long', year: 'numeric' },
  dayMonth: { day: '2-digit', month: 'short' },
  time: { hour: '2-digit', minute: '2-digit' },
  // Vardiya izgarasinin gun basligi: gun adi dilden gelir (sabit metin yok).
  weekdayShort: { weekday: 'short' },
};
