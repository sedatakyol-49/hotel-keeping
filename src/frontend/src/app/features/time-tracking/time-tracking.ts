import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { Card } from '../../shared/ui/card/card';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/**
 * TimeTrackingPage — modul iskeleti.
 * Veri katmani (`timeTracking.store.ts` + `core/api`) backend uc noktalari
 * hazir oldugunda eklenecektir; yerlesim, i18n ve RBAC koruması simdiden gecerlidir.
 */
@Component({
  selector: 'hc-time-tracking',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, EmptyState, Card],
  template: `
    <div class="space-y-6">
      <hc-page-header
        titleKey="timeTracking.title"
        subtitleKey="timeTracking.subtitle"
        eyebrowKey="nav.section.staff"
      />

      <hc-empty-state
        titleKey="timeTracking.empty.title"
        descriptionKey="timeTracking.empty.description"
        eyebrowKey="common.underDevelopment"
      />

      <hc-card titleKey="common.underDevelopment" headingId="time-tracking-pending">
        <p class="text-sm text-ink-muted">{{ 'common.apiPending' | translate }}</p>
      </hc-card>
    </div>
  `,
})
export class TimeTrackingPage {}
