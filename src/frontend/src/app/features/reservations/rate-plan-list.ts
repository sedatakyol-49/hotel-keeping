import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import type { RatePlanResponse } from '../../core/models/rate-plan.model';
import { RESERVATION_CHANNEL_LABEL_KEYS } from '../../core/models/reservation.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { isIsoDate } from '../../shared/forms/date-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Badge } from '../../shared/ui/badge/badge';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { RoomTypesStore } from '../rooms/room-types.store';
import { RatePlansStore } from './rate-plans.store';

/**
 * Fiyat plani listesi (`GET /rate-plans`).
 *
 * Sozlesme farki ekranda acikca yazilir: `validFrom`/`validTo` **kapali**
 * araliktir (`validTo` **dahil**), konaklama araligi ise yari aciktir. Bu
 * ayrimi gizlemek "son gun neden ucretlendirilmedi?" sorusunu dogururdu.
 *
 * `channel: null` = **tum kanallar**; kanala ozel plan ile "tum kanallar" plani
 * cakisma saymaz cunku fiyat seciminde kanala ozel plan her zaman once gelir.
 *
 * Silme **hard delete**'tir: kullanilan plan silinemez (409) → kullanici
 * `isActive: false` yoluna yonlendirilir.
 */
@Component({
  selector: 'hc-rate-plan-list',
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
    Badge,
    HasPermissionDirective,
  ],
  templateUrl: './rate-plan-list.html',
})
export class RatePlanListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(RatePlansStore);
  protected readonly roomTypes = inject(RoomTypesStore);

  protected readonly managePermission = PERMISSIONS.RatesManage;
  protected readonly channelLabelKeys = RESERVATION_CHANNEL_LABEL_KEYS;

  protected readonly confirmingId = signal<string | null>(null);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi (`?roomTypeId=&date=`). */
  protected readonly filters = computed(() => {
    const params = this.queryParams();
    const roomTypeId = params.get('roomTypeId')?.trim();
    const date = params.get('date')?.trim();
    return {
      roomTypeId: roomTypeId ? roomTypeId : null,
      date: date && isIsoDate(date) ? date : null,
    };
  });

  protected readonly deleteErrorKey = computed(() => {
    const error = this.store.deleteError();
    if (!error) {
      return null;
    }
    return error.status === 409 ? 'ratePlans.delete.conflict' : error.messageKey;
  });

  constructor() {
    void this.roomTypes.load();
    effect(() => {
      this.confirmingId.set(null);
      void this.store.load(this.filters());
    });
  }

  protected applyFilters(roomTypeId: string, date: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        roomTypeId: roomTypeId || null,
        date: isIsoDate(date) ? date : null,
      },
    });
  }

  protected resetFilters(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { roomTypeId: null, date: null },
    });
  }

  protected retry(): void {
    void this.store.reload();
  }

  protected askDelete(plan: RatePlanResponse): void {
    this.store.clearDeleteError();
    this.confirmingId.set(plan.id);
  }

  protected cancelDelete(): void {
    this.confirmingId.set(null);
  }

  protected async remove(plan: RatePlanResponse): Promise<void> {
    const error = await this.store.remove(plan.id);
    if (error === null) {
      this.confirmingId.set(null);
    }
  }
}
