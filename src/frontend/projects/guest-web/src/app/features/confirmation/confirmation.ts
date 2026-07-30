import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { PageIntro } from '../../shared/ui/page-intro/page-intro';

/**
 * Rezervasyon onayi. Rezervasyon numarasi mono/tabular gosterilir — misafir
 * bu numarayi telefonda okur, karakterlerin birbirine karismamasi islevseldir.
 * Render modu istemci; sayfa kisiye ozeldir ve `noindex` tasir.
 */
@Component({
  selector: 'hcg-confirmation-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageIntro],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'confirmation.eyebrow' | translate"
        [heading]="'confirmation.title' | translate"
        [lede]="'confirmation.lede' | translate"
      />

      <dl class="mt-10 max-w-measure border-t border-rule">
        <div class="flex items-baseline justify-between gap-6 border-b border-rule py-4">
          <dt class="label-mono text-ink-muted">{{ 'confirmation.reference' | translate }}</dt>
          <dd class="numeric text-lg" data-testid="confirmation-reference">{{ reference() }}</dd>
        </div>
      </dl>

      <p class="mt-10 max-w-measure text-sm text-ink-muted" data-testid="pending-note">
        {{ 'common.pendingApi' | translate }}
      </p>
    </div>
  `,
})
export class ConfirmationPage {
  /** `/{lang}/confirmation/:reference` rota parametresi. */
  readonly reference = input.required<string>();
}
