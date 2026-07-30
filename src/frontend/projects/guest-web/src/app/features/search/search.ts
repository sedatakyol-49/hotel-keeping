import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { PageIntro } from '../../shared/ui/page-intro/page-intro';

/**
 * Arama sonuclari — bu turda yalnizca **duzen**.
 *
 * Musaitlik ve fiyat sorgusu API sozlesmesi ciktiginda baglanacak. Sayfa
 * `noindex, follow` ile isaretlidir (rota `data`): sorgu bagimli sonuc
 * sayfalari dizine eklenirse binlerce ince icerikli varyant uretilir; buna
 * karsilik baglantilari izlenmelidir ki oda tipi sayfalari kesfedilsin.
 */
@Component({
  selector: 'hcg-search-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageIntro],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'search.eyebrow' | translate"
        [heading]="'search.title' | translate"
        [lede]="'search.lede' | translate"
      />
      <p class="mt-10 max-w-measure text-sm text-ink-muted" data-testid="pending-note">
        {{ 'common.pendingApi' | translate }}
      </p>
    </div>
  `,
})
export class SearchPage {}
