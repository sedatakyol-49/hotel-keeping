import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { INVOICE_STATUS_LABEL_KEYS, type InvoiceStatus } from '../../core/models/invoice.model';
import { Badge, type BadgeTone } from '../../shared/ui/badge/badge';

/**
 * Durum -> defter paletindeki ton (rezervasyon/izin rozetleriyle **ayni**
 * yaklasim; yeni renk uretilmez).
 *
 * `Draft` bilincli olarak `brass` + **kesikli cerceve**dir: taslak henuz belge
 * degildir (numarasi yoktur, serbestce degistirilebilir) ve bu gecicilik
 * gorsel olarak da okunmalidir. `Cancelled` de kesiklidir.
 */
const STATUS_TONES: Readonly<Record<InvoiceStatus, BadgeTone>> = {
  Draft: 'brass',
  Finalized: 'navy',
  Paid: 'success',
  Cancelled: 'danger',
};

const DASHED_STATUSES: readonly InvoiceStatus[] = ['Draft', 'Cancelled'];

/** Fatura durum rozeti — tablo ve mobil kart ayni gorunumu paylasir. */
@Component({
  selector: 'hc-invoice-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Badge],
  template: `
    <hc-badge [tone]="tone()" [dashed]="dashed()">
      {{ labelKey() | translate }}
    </hc-badge>
  `,
})
export class InvoiceStatusBadge {
  readonly status = input.required<InvoiceStatus>();

  protected readonly tone = computed<BadgeTone>(() => STATUS_TONES[this.status()]);
  protected readonly dashed = computed(() => DASHED_STATUSES.includes(this.status()));
  protected readonly labelKey = computed(() => INVOICE_STATUS_LABEL_KEYS[this.status()]);
}
