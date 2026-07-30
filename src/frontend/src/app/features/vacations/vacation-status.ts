import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { VACATION_STATUS_LABEL_KEYS, type VacationStatus } from '../../core/models/vacation.model';
import { Badge, type BadgeTone } from '../../shared/ui/badge/badge';

/**
 * Durum -> defter paletindeki ton (housekeeping rozetiyle ayni yaklasim; yeni
 * renk uretilmez). `brass` tonu bekleyen/opsiyon durumlari icin ayrilmistir,
 * bu yuzden `Pending` oradan beslenir.
 */
const STATUS_TONES: Readonly<Record<VacationStatus, BadgeTone>> = {
  Pending: 'brass',
  Approved: 'success',
  Rejected: 'danger',
  Cancelled: 'neutral',
};

/**
 * Izin talebi durum rozeti — tablo ve mobil kart ayni gorunumu paylasir.
 * `Cancelled` kesikli cerceve ile isaretlenir (artik gecerli degil).
 */
@Component({
  selector: 'hc-vacation-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Badge],
  template: `
    <hc-badge [tone]="tone()" [dashed]="status() === 'Cancelled'">
      {{ labelKey() | translate }}
    </hc-badge>
  `,
})
export class VacationStatusBadge {
  readonly status = input.required<VacationStatus>();

  protected readonly tone = computed<BadgeTone>(() => STATUS_TONES[this.status()]);
  protected readonly labelKey = computed(() => VACATION_STATUS_LABEL_KEYS[this.status()]);
}
