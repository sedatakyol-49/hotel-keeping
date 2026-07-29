import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Bos durum blogu — ikon/illustrasyon yok, yalnizca tipografi ve cetvel cizgisi.
 */
@Component({
  selector: 'hc-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div class="border border-dashed border-rule-strong bg-paper px-5 py-8 text-center sm:py-12">
      <p class="eyebrow">{{ eyebrowKey() | translate }}</p>
      <h3 class="mt-3 font-serif text-2xl text-ink">{{ titleKey() | translate }}</h3>
      @if (descriptionKey(); as description) {
        <p class="mx-auto mt-2 max-w-prose text-sm text-ink-muted">
          {{ description | translate }}
        </p>
      }
      <div class="mt-5 empty:hidden">
        <ng-content />
      </div>
    </div>
  `,
})
export class EmptyState {
  readonly titleKey = input.required<string>();
  readonly descriptionKey = input<string | null>(null);
  readonly eyebrowKey = input('common.noData');
}
