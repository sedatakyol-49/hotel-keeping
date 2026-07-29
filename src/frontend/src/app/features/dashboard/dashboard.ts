import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import { CurrentHotelService } from '../../core/services/current-hotel.service';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { Card } from '../../shared/ui/card/card';
import { PageHeader } from '../../shared/ui/page-header/page-header';

interface KpiTile {
  readonly labelKey: string;
  /** Deger backend'den gelene kadar em-dash gosterilir. */
  readonly value: string;
}

/**
 * Gunluk ozet. Finansal kartlar `Reports.View` izniyle korunur; Housekeeping
 * rolu ciro/fiyat gormez (mimari §7).
 */
@Component({
  selector: 'hc-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, Card, HasPermissionDirective],
  template: `
    <div class="space-y-6">
      <hc-page-header
        titleKey="dashboard.title"
        subtitleKey="dashboard.subtitle"
        eyebrowKey="nav.section.overview"
      >
        <p slot="actions" class="label-mono text-ink-muted">
          {{ currentHotel.hotel()?.name ?? ('hotel.allHotels' | translate) }}
        </p>
      </hc-page-header>

      <ul class="grid grid-cols-1 gap-px border border-rule bg-rule sm:grid-cols-2 xl:grid-cols-3">
        @for (tile of openTiles; track tile.labelKey) {
          <li class="bg-paper-raised px-4 py-5">
            <p class="eyebrow">{{ tile.labelKey | translate }}</p>
            <p class="mt-2 numeric text-3xl text-ink">{{ tile.value }}</p>
          </li>
        }
        @for (tile of financialTiles; track tile.labelKey) {
          <li *hcHasPermission="reportsPermission" class="bg-paper-raised px-4 py-5">
            <p class="eyebrow">{{ tile.labelKey | translate }}</p>
            <p class="mt-2 numeric text-3xl text-ink">{{ tile.value }}</p>
          </li>
        }
      </ul>

      <hc-card titleKey="dashboard.empty.title" headingId="dashboard-pending">
        <p class="text-sm text-ink-muted">{{ 'dashboard.empty.description' | translate }}</p>
      </hc-card>
    </div>
  `,
})
export class DashboardPage {
  protected readonly currentHotel = inject(CurrentHotelService);
  protected readonly reportsPermission = PERMISSIONS.ReportsView;

  /** Her role acik operasyonel gostergeler. */
  protected readonly openTiles: readonly KpiTile[] = [
    { labelKey: 'dashboard.kpi.occupancy', value: '—' },
    { labelKey: 'dashboard.kpi.arrivals', value: '—' },
    { labelKey: 'dashboard.kpi.departures', value: '—' },
    { labelKey: 'dashboard.kpi.roomsToClean', value: '—' },
  ];

  /** Finansal gostergeler — `Reports.View` izni gerekir. */
  protected readonly financialTiles: readonly KpiTile[] = [
    { labelKey: 'dashboard.kpi.revenue', value: '—' },
    { labelKey: 'dashboard.kpi.openInvoices', value: '—' },
  ];
}
