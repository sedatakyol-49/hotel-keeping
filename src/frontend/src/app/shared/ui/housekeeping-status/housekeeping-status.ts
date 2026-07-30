import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import {
  HOUSEKEEPING_STATUS_LABEL_KEYS,
  type HousekeepingStatus,
} from '../../../core/models/room.model';
import { Badge, type BadgeTone } from '../badge/badge';

/** Durum -> defter paletindeki ton. Yeni renk uretilmez. */
const STATUS_TONES: Readonly<Record<HousekeepingStatus, BadgeTone>> = {
  Clean: 'success',
  Dirty: 'copper',
  Inspected: 'navy',
  OutOfOrder: 'danger',
};

/**
 * Housekeeping durum rozeti — oda listesi ve kat panosu ayni gorunumu paylasir.
 * `OutOfOrder` kesikli cerceve ile isaretlenir (kullanim disi).
 */
@Component({
  selector: 'hc-housekeeping-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Badge],
  template: `
    <hc-badge [tone]="tone()" [dashed]="status() === 'OutOfOrder'">
      {{ labelKey() | translate }}
    </hc-badge>
  `,
})
export class HousekeepingStatusBadge {
  readonly status = input.required<HousekeepingStatus>();

  protected readonly tone = computed<BadgeTone>(() => STATUS_TONES[this.status()]);
  protected readonly labelKey = computed(() => HOUSEKEEPING_STATUS_LABEL_KEYS[this.status()]);
}
