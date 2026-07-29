import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { Card } from '../../shared/ui/card/card';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/**
 * ReservationsPage — modul iskeleti.
 * Veri katmani (`reservations.store.ts` + `core/api`) backend uc noktalari
 * hazir oldugunda eklenecektir; yerlesim, i18n ve RBAC koruması simdiden gecerlidir.
 */
@Component({
  selector: 'hc-reservations',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, EmptyState, Card],
  template: `
    <div class="space-y-6">
      <hc-page-header
        titleKey="reservations.title"
        subtitleKey="reservations.subtitle"
        eyebrowKey="nav.section.operations"
      />

      <hc-empty-state
        titleKey="reservations.empty.title"
        descriptionKey="reservations.empty.description"
        eyebrowKey="common.underDevelopment"
      />

      <hc-card titleKey="common.underDevelopment" headingId="reservations-pending">
        <p class="text-sm text-ink-muted">{{ 'common.apiPending' | translate }}</p>
      </hc-card>
    </div>
  `,
})
export class ReservationsPage {}
