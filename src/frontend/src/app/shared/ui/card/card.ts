import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Defter sayfasi kartı: 1px cetvel cerceve, kagit zemin, kose yuvarlama yok.
 * Baslik verilirse ustte ince bir ayrac cizgisiyle ayrilir.
 */
@Component({
  selector: 'hc-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <section
      class="border border-rule bg-paper-raised"
      [attr.aria-labelledby]="titleKey() ? headingId() : null"
    >
      @if (titleKey(); as title) {
        <header class="flex items-baseline justify-between gap-3 border-b border-rule px-4 py-3">
          <h2 [id]="headingId()" class="label-mono text-ink">{{ title | translate }}</h2>
          @if (metaKey(); as meta) {
            <p class="eyebrow">{{ meta | translate }}</p>
          }
        </header>
      }
      <div [class]="padded() ? 'p-4' : ''">
        <ng-content />
      </div>
    </section>
  `,
})
export class Card {
  /** Baslik i18n anahtari (opsiyonel). */
  readonly titleKey = input<string | null>(null);
  /** Baslik saginda gosterilen ikincil bilgi i18n anahtari. */
  readonly metaKey = input<string | null>(null);
  readonly padded = input(true);
  /** `aria-labelledby` icin benzersiz id. */
  readonly headingId = input<string | null>(null);
}
