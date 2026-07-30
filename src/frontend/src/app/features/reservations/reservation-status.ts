import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import {
  RESERVATION_STATUS_LABEL_KEYS,
  type ReservationStatus,
} from '../../core/models/reservation.model';
import { Badge, type BadgeTone } from '../../shared/ui/badge/badge';

/**
 * Durum -> defter paletindeki ton (izin/housekeeping rozetiyle **ayni**
 * yaklasim; yeni renk uretilmez).
 *
 * `brass` tonu bilincli olarak opsiyon/bekleyen durumlara ayrilmistir, bu yuzden
 * `Option` oradan beslenir ve ayrica **kesikli cerceve** ile isaretlenir
 * (mimari §4.3: opsiyon = kesikli cizgi). `NoShow` da kesiklidir: taahhut
 * gerceklesmemistir.
 */
const STATUS_TONES: Readonly<Record<ReservationStatus, BadgeTone>> = {
  Option: 'brass',
  Confirmed: 'navy',
  CheckedIn: 'success',
  CheckedOut: 'neutral',
  Cancelled: 'danger',
  NoShow: 'danger',
};

/** Kesikli cerceve tasiyan durumlar (gerceklesmemis/gecici taahhut). */
const DASHED_STATUSES: readonly ReservationStatus[] = ['Option', 'NoShow', 'Cancelled'];

/**
 * Rezervasyon durum rozeti — tablo, mobil kart ve doluluk izgarasi ayni
 * gorunumu paylasir.
 */
@Component({
  selector: 'hc-reservation-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Badge],
  template: `
    <hc-badge [tone]="tone()" [dashed]="dashed()">
      {{ labelKey() | translate }}
    </hc-badge>
  `,
})
export class ReservationStatusBadge {
  readonly status = input.required<ReservationStatus>();

  protected readonly tone = computed<BadgeTone>(() => STATUS_TONES[this.status()]);
  protected readonly dashed = computed(() => DASHED_STATUSES.includes(this.status()));
  protected readonly labelKey = computed(() => RESERVATION_STATUS_LABEL_KEYS[this.status()]);
}

/**
 * Doluluk izgarasi cubugunun sinif eslesmesi. Rozetle **ayni** ton mantigini
 * izler ki kullanici iki ekranda ayni rengi ayni anlamla gorsun.
 * `Cancelled`/`NoShow` izgarada hic gorunmez (odayi bloke etmezler).
 */
export const OCCUPANCY_BAR_CLASSES: Readonly<Record<ReservationStatus, string>> = {
  Option: 'border-dashed border-brass bg-brass-tint text-brass',
  Confirmed: 'border-navy bg-navy-tint text-navy',
  CheckedIn: 'border-success bg-success-tint text-success',
  CheckedOut: 'border-rule-strong bg-paper-sunken text-ink-muted',
  Cancelled: 'border-dashed border-danger bg-danger-tint text-danger',
  NoShow: 'border-dashed border-danger bg-danger-tint text-danger',
};
