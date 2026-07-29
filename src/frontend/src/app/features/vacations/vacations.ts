import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { Card } from '../../shared/ui/card/card';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/**
 * VacationsPage — modul iskeleti.
 * Veri katmani (`vacations.store.ts` + `core/api`) backend uc noktalari
 * hazir oldugunda eklenecektir; yerlesim, i18n ve RBAC koruması simdiden gecerlidir.
 */
@Component({
  selector: 'hc-vacations',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, EmptyState, Card],
  template: `
    <div class="space-y-6">
      <hc-page-header
        titleKey="vacations.title"
        subtitleKey="vacations.subtitle"
        eyebrowKey="nav.section.staff"
      />

      <hc-empty-state
        titleKey="vacations.empty.title"
        descriptionKey="vacations.empty.description"
        eyebrowKey="common.underDevelopment"
      />

      <hc-card titleKey="common.underDevelopment" headingId="vacations-pending">
        <p class="text-sm text-ink-muted">{{ 'common.apiPending' | translate }}</p>
      </hc-card>
    </div>
  `,
})
export class VacationsPage {}
