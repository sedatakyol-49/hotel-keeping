import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import {
  HOUSEKEEPING_STATUSES,
  HOUSEKEEPING_STATUS_LABEL_KEYS,
  ROOM_LIMITS,
  isHousekeepingStatus,
  type RoomListQuery,
  type RoomResponse,
} from '../../core/models/room.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { parseInteger } from '../../shared/forms/numeric-validators';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { HousekeepingStatusBadge } from '../../shared/ui/housekeeping-status/housekeeping-status';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import {
  ROOM_PAGE_SIZE_OPTIONS,
  parseRoomListQuery,
  roomListQueryToParams,
  withFilterChange,
} from './room-list-query';
import { RoomTypesStore } from './room-types.store';
import { RoomsStore } from './rooms.store';

/**
 * Oda listesi (`GET /rooms`).
 *
 * Filtreler ve sayfa **URL sorgu parametrelerinde** tutulur: sayfa yenilenince
 * durum korunur, baglanti paylasilabilir ve geri/ileri dugmeleri dogru calisir.
 * Masaustunde yogun tablo, mobilde kart listesi — ikisi de ayni signal store'u okur.
 */
@Component({
  selector: 'hc-room-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
    HasPermissionDirective,
    HousekeepingStatusBadge,
  ],
  templateUrl: './room-list.html',
})
export class RoomListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(RoomsStore);
  protected readonly roomTypes = inject(RoomTypesStore);

  protected readonly managePermission = PERMISSIONS.RoomsManage;
  protected readonly statuses = HOUSEKEEPING_STATUSES;
  protected readonly statusLabelKeys = HOUSEKEEPING_STATUS_LABEL_KEYS;
  protected readonly pageSizeOptions = ROOM_PAGE_SIZE_OPTIONS;
  protected readonly floorLimits = ROOM_LIMITS;

  /** Satir bazli silme onayi — ayri bir diyalog bileseni gerekmez. */
  protected readonly pendingDeleteId = signal<string | null>(null);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi; gecersiz degerler varsayilana duser. */
  protected readonly query = computed(() => parseRoomListQuery(this.queryParams()));

  protected readonly deleteErrorKey = computed(() => {
    const error = this.store.deleteError();
    if (!error) {
      return null;
    }
    return error.status === 409 ? 'rooms.delete.conflict' : error.messageKey;
  });

  constructor() {
    // Sorgu degistikce liste yenilenir (ilk yukleme dahil).
    effect(() => {
      const query = this.query();
      this.pendingDeleteId.set(null);
      void this.store.load(query);
    });

    // Oda tipi filtresi icin secenekler.
    void this.roomTypes.load();
  }

  protected applyFromForm(
    search: string,
    floor: string,
    roomTypeId: string,
    housekeepingStatus: string,
  ): void {
    this.applyFilters({
      search: search.trim() || null,
      floor: parseInteger(floor),
      roomTypeId: roomTypeId || null,
      housekeepingStatus: isHousekeepingStatus(housekeepingStatus) ? housekeepingStatus : null,
    });
  }

  protected onPageSizeChange(event: Event): void {
    const pageSize = parseInteger((event.target as HTMLSelectElement).value);
    if (pageSize !== null) {
      this.applyFilters({ pageSize });
    }
  }

  protected resetFilters(): void {
    void this.navigate({ page: 1, pageSize: this.query().pageSize });
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

  protected requestDelete(id: string): void {
    this.store.clearDeleteError();
    this.pendingDeleteId.set(id);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected async confirmDelete(room: RoomResponse): Promise<void> {
    const error = await this.store.remove(room.id);
    if (error === null) {
      this.pendingDeleteId.set(null);
    }
  }

  /** Filtre degisikligi her zaman ilk sayfaya doner. */
  private applyFilters(changes: Partial<Omit<RoomListQuery, 'page'>>): void {
    void this.navigate(withFilterChange(this.query(), changes));
  }

  private navigate(query: RoomListQuery): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: roomListQueryToParams(query),
    });
  }
}
