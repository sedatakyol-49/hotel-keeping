import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { MediaFrame } from '../../shared/ui/media-frame/media-frame';
import { PageIntro } from '../../shared/ui/page-intro/page-intro';

/**
 * Oda tipi detayi — sitenin SEO acisindan en degerli sayfa turu.
 *
 * `slug` rota parametresi `withComponentInputBinding()` sayesinde dogrudan
 * girdi olarak baglanir. Icerik (aciklama, donanim, fiyat) sozlesme belgesinden
 * sonra gelir; gorsel kutulari simdiden dogru oranda ayrilmistir.
 */
@Component({
  selector: 'hcg-room-type-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, MediaFrame, PageIntro],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'roomType.eyebrow' | translate"
        [heading]="'roomType.title' | translate"
        [lede]="'roomType.lede' | translate"
      />

      <p class="mt-6 label-mono text-ink-faint" data-testid="room-slug">{{ slug() }}</p>

      <div class="mt-10 grid gap-6 md:grid-cols-3">
        <div class="md:col-span-2">
          <hcg-media-frame
            [width]="1600"
            [height]="1067"
            [priority]="true"
            [alt]="'roomType.imageAlt' | translate"
          />
        </div>
        <div class="grid gap-6">
          <hcg-media-frame [width]="800" [height]="800" [alt]="'roomType.imageAlt' | translate" />
          <hcg-media-frame [width]="800" [height]="800" [alt]="'roomType.imageAlt' | translate" />
        </div>
      </div>

      <p class="mt-10 max-w-measure text-sm text-ink-muted" data-testid="pending-note">
        {{ 'common.pendingApi' | translate }}
      </p>
    </div>
  `,
})
export class RoomTypePage {
  /** `/{lang}/rooms/:slug` rota parametresi. */
  readonly slug = input.required<string>();
}
