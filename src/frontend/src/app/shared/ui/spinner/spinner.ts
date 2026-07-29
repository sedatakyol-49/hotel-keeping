import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Yukleniyor gostergesi — ikon yerine ince bir cetvel cizgisi kayar.
 * Ekran okuyucular icin metin `common.loading` anahtarindan gelir.
 */
@Component({
  selector: 'hc-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div class="flex items-center gap-3" role="status" aria-live="polite">
      <span class="hc-rail" aria-hidden="true"></span>
      @if (showLabel()) {
        <span class="eyebrow">{{ labelKey() | translate }}</span>
      } @else {
        <span class="sr-only">{{ labelKey() | translate }}</span>
      }
    </div>
  `,
  styles: `
    .hc-rail {
      position: relative;
      display: block;
      width: 4rem;
      height: 1px;
      overflow: hidden;
      background-color: var(--color-rule);
    }

    .hc-rail::after {
      content: '';
      position: absolute;
      inset-block: 0;
      width: 40%;
      background-color: var(--color-copper);
      animation: hc-rail-slide 1.1s linear infinite;
    }

    @keyframes hc-rail-slide {
      from {
        transform: translateX(-100%);
      }
      to {
        transform: translateX(250%);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .hc-rail::after {
        animation: none;
        width: 100%;
      }
    }
  `,
})
export class Spinner {
  readonly labelKey = input('common.loading');
  readonly showLabel = input(false);
}
