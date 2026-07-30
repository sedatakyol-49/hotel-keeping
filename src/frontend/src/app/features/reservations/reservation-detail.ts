import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import {
  FOLIO_LINE_TYPE_LABEL_KEYS,
  RESERVATION_CHANNEL_LABEL_KEYS,
  RESERVATION_LIMITS,
} from '../../core/models/reservation.model';
import { AuthStore } from '../../core/state/auth.store';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { ReservationDetailStore } from './reservation-detail.store';
import { ReservationStatusBadge } from './reservation-status';
import type { ReservationAction } from './reservations.store';

/**
 * Rezervasyon detayi + aksiyonlar + folio.
 *
 * ### Aksiyon gorunurlugu iki kapiya baglidir
 * 1. **Durum makinesi** (`ReservationDetailStore.canCheckIn` vb.): gecersiz
 *    gecisin dugmesi **hic render edilmez**. Ornek: `CheckedIn` bir
 *    rezervasyonda "iptal" dugmesi yoktur (sozlesme: `CheckedIn` → yalnizca
 *    `CheckedOut`). Sunucu da 409 dondururdu; kullaniciya yasak yolu gostermeyiz.
 * 2. **Izin** (`*hcHasPermission`): check-in/check-out/no-show
 *    `Reservations.CheckInOut`, iptal `Reservations.Create` gerektirir.
 *
 * ### Check-out yan etkisi
 * Check-out odanin temizlik durumunu **otomatik `Dirty`** yapar (rezervasyonla
 * ayni `SaveChanges`). Bu kullaniciya aksiyonun yaninda kucuk bir bilgi metniyle
 * ve islem sonrasi onayla soylenir — housekeeping ekraninda "neden kirli?"
 * sorusu olusmasin.
 */
@Component({
  selector: 'hc-reservation-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    MoneyPipe,
    PageHeader,
    TableShell,
    Spinner,
    Button,
    ReservationStatusBadge,
    HasPermissionDirective,
  ],
  templateUrl: './reservation-detail.html',
})
export class ReservationDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly authStore = inject(AuthStore);

  protected readonly store = inject(ReservationDetailStore);

  protected readonly checkInOutPermission = PERMISSIONS.ReservationsCheckInOut;
  protected readonly createPermission = PERMISSIONS.ReservationsCreate;
  protected readonly invoicesCreatePermission = PERMISSIONS.InvoicesCreate;
  protected readonly channelLabelKeys = RESERVATION_CHANNEL_LABEL_KEYS;
  protected readonly folioTypeLabelKeys = FOLIO_LINE_TYPE_LABEL_KEYS;
  protected readonly reasonMaxLength = RESERVATION_LIMITS.cancelReasonMaxLength;

  /** Iptal gerekcesi paneli acik mi. */
  protected readonly cancelPanelOpen = signal(false);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  protected readonly reservationId = computed(() => this.params().get('id') ?? '');

  /** Ekranda gosterilebilecek aksiyon var mi (yoksa blok hic cizilmez). */
  protected readonly hasCheckInOutActions = computed(
    () => this.store.canCheckIn() || this.store.canCheckOut() || this.store.canMarkNoShow(),
  );

  /** Nihai durumda hicbir gecis mumkun degildir — bilgi metni gosterilir. */
  protected readonly isFinal = computed(
    () =>
      this.store.reservation() !== null &&
      !this.store.canCheckIn() &&
      !this.store.canCheckOut() &&
      !this.store.canCancel() &&
      !this.store.canMarkNoShow(),
  );

  protected readonly canCreateInvoice = computed(() =>
    this.authStore.hasPermission(PERMISSIONS.InvoicesCreate),
  );

  /** Aksiyon hatasini anlamli bir mesaja cevirir (409 = gecersiz gecis). */
  protected readonly actionErrorKey = computed(() => {
    const error = this.store.actionError();
    if (!error) {
      return null;
    }
    if (error.status === 409) {
      return 'reservations.actions.conflict';
    }
    if (error.status === 403) {
      return 'errors.forbidden';
    }
    return error.messageKey;
  });

  constructor() {
    effect(() => {
      const id = this.reservationId();
      this.cancelPanelOpen.set(false);
      if (id) {
        void this.store.load(id);
      }
    });
  }

  protected retry(): void {
    void this.store.reload();
  }

  protected openCancelPanel(): void {
    this.store.clearActionError();
    this.cancelPanelOpen.set(true);
  }

  protected closeCancelPanel(): void {
    this.cancelPanelOpen.set(false);
  }

  protected async run(action: ReservationAction, reason = ''): Promise<void> {
    const error = await this.store.run(action, reason.trim() || null);
    if (error === null) {
      this.cancelPanelOpen.set(false);
    }
  }

  protected dismissLastAction(): void {
    this.store.clearLastAction();
  }
}
