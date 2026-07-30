import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import {
  RESERVATION_CHANNELS,
  RESERVATION_CHANNEL_LABEL_KEYS,
  RESERVATION_STATUSES,
  RESERVATION_STATUS_LABEL_KEYS,
  isReservationChannel,
  isReservationStatus,
  type ReservationListQuery,
} from '../../core/models/reservation.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { isIsoDate } from '../../shared/forms/date-validators';
import { parseInteger } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { RoomOptionsStore } from '../rooms/room-options.store';
import { ReservationStatusBadge } from './reservation-status';
import {
  RESERVATION_PAGE_SIZE_OPTIONS,
  parseReservationListQuery,
  reservationListQueryToParams,
  withReservationFilterChange,
} from './reservation-list-query';
import { ReservationsStore } from './reservations.store';

/**
 * Rezervasyon listesi (`GET /reservations`).
 *
 * Filtreler ve sayfa **URL sorgu parametrelerinde** tutulur (izin/oda
 * listeleriyle ayni desen); masaustunde yogun tablo, mobilde kart listesi —
 * ikisi de ayni signal store'u okur.
 *
 * Tarih filtresi **kesisen** konaklamalari secer (`from < checkOut && checkIn < to`),
 * kapsayanlari degil: 10–12 arasi konaklama, 11–13 filtresinde de gorunur.
 * Ekran bunu kisa bir bilgi metniyle acikilar.
 */
@Component({
  selector: 'hc-reservation-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    MoneyPipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
    ReservationStatusBadge,
    HasPermissionDirective,
  ],
  templateUrl: './reservation-list.html',
})
export class ReservationListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(ReservationsStore);
  protected readonly rooms = inject(RoomOptionsStore);

  protected readonly createPermission = PERMISSIONS.ReservationsCreate;
  protected readonly ratesViewPermission = PERMISSIONS.RatesView;
  protected readonly statuses = RESERVATION_STATUSES;
  protected readonly statusLabelKeys = RESERVATION_STATUS_LABEL_KEYS;
  protected readonly channels = RESERVATION_CHANNELS;
  protected readonly channelLabelKeys = RESERVATION_CHANNEL_LABEL_KEYS;
  protected readonly pageSizeOptions = RESERVATION_PAGE_SIZE_OPTIONS;

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi; gecersiz degerler varsayilana duser. */
  protected readonly query = computed(() => parseReservationListQuery(this.queryParams()));

  constructor() {
    effect(() => {
      void this.store.load(this.query());
    });

    // Oda filtresi icin envanter (`Rooms.View` gerekir; izin yoksa 403 doner
    // ve ekran filtreyi gizler).
    void this.rooms.load();
  }

  protected applyFromForm(
    status: string,
    channel: string,
    roomId: string,
    from: string,
    to: string,
    search: string,
  ): void {
    this.applyFilters({
      status: isReservationStatus(status) ? status : null,
      channel: isReservationChannel(channel) ? channel : null,
      roomId: roomId || null,
      from: isIsoDate(from) ? from : null,
      to: isIsoDate(to) ? to : null,
      search: search.trim() || null,
    });
  }

  protected onPageSizeChange(event: Event): void {
    const pageSize = parseInteger((event.target as HTMLSelectElement).value);
    if (pageSize !== null) {
      this.applyFilters({ pageSize });
    }
  }

  protected resetFilters(): void {
    void this.navigate({
      ...this.query(),
      page: 1,
      status: null,
      channel: null,
      roomId: null,
      guestId: null,
      from: null,
      to: null,
      search: null,
    });
  }

  /** Misafir filtresi yalnizca baska ekrandan gelen baglantiyla kurulur. */
  protected clearGuestFilter(): void {
    this.applyFilters({ guestId: null });
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.store.totalPages()) {
      return;
    }
    void this.navigate({ ...this.query(), page });
  }

  protected retry(): void {
    void this.store.reload();
  }

  private applyFilters(changes: Partial<Omit<ReservationListQuery, 'page'>>): void {
    void this.navigate(withReservationFilterChange(this.query(), changes));
  }

  private navigate(query: ReservationListQuery): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: reservationListQueryToParams(query),
    });
  }
}
