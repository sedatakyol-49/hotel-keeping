import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import type { RoomTypeResponse } from '../../core/models/room-type.model';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { RoomTypesStore } from './room-types.store';

/**
 * Oda tipi listesi (`GET /room-types`). Rota `Rooms.Manage` ile korundugu icin
 * yazma aksiyonlari ayrica gizlenmez; fiyat gosterimi `hcMoney` ile locale'e uyar.
 */
@Component({
  selector: 'hc-room-type-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    MoneyPipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
  ],
  templateUrl: './room-type-list.html',
})
export class RoomTypeListPage {
  protected readonly store = inject(RoomTypesStore);

  protected readonly pendingDeleteId = signal<string | null>(null);

  protected readonly deleteErrorKey = computed(() => {
    const error = this.store.deleteError();
    if (!error) {
      return null;
    }
    return error.status === 409 ? 'rooms.types.delete.conflict' : error.messageKey;
  });

  constructor() {
    // Yazma islemlerinden sonra donuldugu icin her acilista tazelenir.
    void this.store.load(true);
  }

  protected retry(): void {
    void this.store.load(true);
  }

  protected requestDelete(id: string): void {
    this.store.clearDeleteError();
    this.pendingDeleteId.set(id);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected async confirmDelete(type: RoomTypeResponse): Promise<void> {
    const error = await this.store.remove(type.id);
    if (error === null) {
      this.pendingDeleteId.set(null);
    }
  }
}
