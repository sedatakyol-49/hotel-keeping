import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { Card } from '../../shared/ui/card/card';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/**
 * InvoicesPage — modul iskeleti.
 * Veri katmani (`invoices.store.ts` + `core/api`) backend uc noktalari
 * hazir oldugunda eklenecektir; yerlesim, i18n ve RBAC koruması simdiden gecerlidir.
 */
@Component({
  selector: 'hc-invoices',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, EmptyState, Card],
  template: `
    <div class="space-y-6">
      <hc-page-header
        titleKey="invoices.title"
        subtitleKey="invoices.subtitle"
        eyebrowKey="nav.section.finance"
      />

      <hc-empty-state
        titleKey="invoices.empty.title"
        descriptionKey="invoices.empty.description"
        eyebrowKey="common.underDevelopment"
      />

      <hc-card titleKey="common.underDevelopment" headingId="invoices-pending">
        <p class="text-sm text-ink-muted">{{ 'common.apiPending' | translate }}</p>
      </hc-card>
    </div>
  `,
})
export class InvoicesPage {}
