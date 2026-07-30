import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore, type AppLanguage } from '@hotelcore/shared';

import { LanguageService } from '../../core/services/language.service';

/**
 * Dil secici — ince cetvel ayraclarla ayrilmis buton grubu (ikon/bayrak yok).
 * Her buton >= 44px dokunmatik hedef sagliyor.
 */
@Component({
  selector: 'hc-language-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div
      class="flex items-stretch divide-x divide-rule border border-rule"
      role="group"
      [attr.aria-label]="'language.switcherLabel' | translate"
    >
      @for (language of store.available; track language) {
        <button
          type="button"
          class="min-h-touch px-3 label-mono transition-colors"
          [class.bg-navy]="store.current() === language"
          [class.text-paper]="store.current() === language"
          [class.text-ink-muted]="store.current() !== language"
          [attr.aria-pressed]="store.current() === language"
          [attr.lang]="language"
          [title]="'language.' + language | translate"
          (click)="select(language)"
        >
          {{ language.toUpperCase() }}
        </button>
      }
    </div>
  `,
})
export class LanguagePicker {
  protected readonly store = inject(LanguageStore);
  private readonly languageService = inject(LanguageService);

  protected select(language: AppLanguage): void {
    this.languageService.use(language);
  }
}
