import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Sayfa basligi: mono "eyebrow" + serif baslik + kisa aciklama,
 * altinda 1px cetvel ayrac. Saga eylem alani projekte edilebilir.
 */
@Component({
  selector: 'hc-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <header class="border-b border-rule pb-4">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div class="min-w-0">
          @if (eyebrowKey(); as eyebrow) {
            <p class="eyebrow">{{ eyebrow | translate }}</p>
          }
          <h1 class="mt-1 font-serif text-3xl leading-tight text-ink sm:text-4xl">
            {{ titleKey() | translate }}
          </h1>
          @if (subtitleKey(); as subtitle) {
            <p class="mt-2 max-w-prose text-sm text-ink-muted">{{ subtitle | translate }}</p>
          }
        </div>
        <div class="flex shrink-0 flex-wrap items-center gap-2 empty:hidden">
          <ng-content select="[slot=actions]" />
        </div>
      </div>
    </header>
  `,
})
export class PageHeader {
  readonly titleKey = input.required<string>();
  readonly subtitleKey = input<string | null>(null);
  readonly eyebrowKey = input<string | null>(null);
}
