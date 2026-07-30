import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { PageIntro } from '../../shared/ui/page-intro/page-intro';

/**
 * Rezervasyon akisi (adim iskeleti).
 *
 * Render modu bilincli olarak **istemci**dir (bkz. app.routes.server.ts):
 * bu sayfa misafirin adini, e-postasini ve odeme baglamini tasir. Sunucuda
 * render etmenin SEO faydasi yoktur (zaten `noindex`), buna karsilik kisisel
 * verinin sunucu belleginde/onbelleginde dolasma yuzeyini buyutur.
 */
@Component({
  selector: 'hcg-booking-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageIntro],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'booking.eyebrow' | translate"
        [heading]="'booking.title' | translate"
        [lede]="'booking.lede' | translate"
      />

      <!-- Adim gostergesi: ikon yok, numara + cetvel. -->
      <ol
        class="mt-10 grid gap-px border border-rule bg-rule sm:grid-cols-3"
        data-testid="booking-steps"
      >
        @for (step of steps; track step.key) {
          <li class="bg-canvas p-5">
            <p class="eyebrow">{{ step.index }}</p>
            <p class="mt-2 text-sm">{{ step.key | translate }}</p>
          </li>
        }
      </ol>

      <p class="mt-10 max-w-measure text-sm text-ink-muted" data-testid="pending-note">
        {{ 'common.pendingApi' | translate }}
      </p>
    </div>
  `,
})
export class BookingPage {
  protected readonly steps = [
    { index: '01', key: 'booking.steps.dates' },
    { index: '02', key: 'booking.steps.guest' },
    { index: '03', key: 'booking.steps.confirm' },
  ] as const;
}
