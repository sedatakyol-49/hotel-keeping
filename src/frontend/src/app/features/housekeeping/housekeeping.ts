import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import {
  HOUSEKEEPING_STATUSES,
  HOUSEKEEPING_STATUS_LABEL_KEYS,
  HOUSEKEEPING_SUMMARY_FIELDS,
  ROOM_LIMITS,
  isHousekeepingStatus,
  type HousekeepingBoardRoom,
  type HousekeepingStatus,
} from '../../core/models/room.model';
import { AuthStore } from '../../core/state/auth.store';
import { LanguageStore } from '@hotelcore/shared';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { HousekeepingStatusBadge } from '../../shared/ui/housekeeping-status/housekeeping-status';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { HousekeepingStore } from './housekeeping.store';

/** Durum -> hucre zemini. Yalnizca mevcut `@theme` tonlari kullanilir. */
const STATUS_CELL_CLASSES: Readonly<Record<HousekeepingStatus, string>> = {
  Clean: 'bg-success-tint',
  Dirty: 'bg-copper-tint',
  Inspected: 'bg-navy-tint',
  OutOfOrder: 'bg-danger-tint',
};

/**
 * Kat bazli housekeeping panosu.
 *
 * Etkilesim modeli: her oda hucresinde bir durum `<select>`'i (klavyeyle
 * kullanilabilir) ve satir ici not duzenleme alani vardir. Degisiklik iyimser
 * uygulanir; hata olursa store geri alir ve `aria-live` bolgesi bunu duyurur.
 * Bu ekranda **para/fiyat alani gosterilmez** (mimari §7).
 */
@Component({
  selector: 'hc-housekeeping',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageHeader, EmptyState, Spinner, Button, HousekeepingStatusBadge],
  templateUrl: './housekeeping.html',
})
export class HousekeepingPage {
  private readonly translate = inject(TranslateService);
  private readonly languageStore = inject(LanguageStore);
  private readonly authStore = inject(AuthStore);

  protected readonly store = inject(HousekeepingStore);

  protected readonly statuses = HOUSEKEEPING_STATUSES;
  protected readonly statusLabelKeys = HOUSEKEEPING_STATUS_LABEL_KEYS;
  protected readonly noteMaxLength = ROOM_LIMITS.noteMaxLength;
  protected readonly editingRoomId = signal<string | null>(null);

  /** Yazma yetkisi — asil kontrol backend policy'sindedir. */
  protected readonly canUpdate = computed(() =>
    this.authStore.hasPermission(PERMISSIONS.HousekeepingUpdate),
  );

  /** `aria-live` bolgesinde okunan metin (durum adi da cevrilir). */
  protected readonly announcementText = computed(() => {
    const announcement = this.store.announcement();
    if (!announcement) {
      return '';
    }
    // Dil degisiminde metnin yeniden hesaplanmasi icin dil sinyali okunur.
    void this.languageStore.current();

    const status = this.translate.instant(
      HOUSEKEEPING_STATUS_LABEL_KEYS[announcement.status],
    ) as string;
    const key =
      announcement.kind === 'updated'
        ? 'housekeeping.board.announce.updated'
        : 'housekeeping.board.announce.failed';

    return this.translate.instant(key, { number: announcement.roomNumber, status }) as string;
  });

  constructor() {
    void this.store.load();
  }

  protected refresh(): void {
    void this.store.load();
  }

  protected countFor(status: HousekeepingStatus): number {
    return this.store.summary()[HOUSEKEEPING_SUMMARY_FIELDS[status]];
  }

  protected cellClass(status: HousekeepingStatus): string {
    return STATUS_CELL_CLASSES[status];
  }

  protected onStatusChange(room: HousekeepingBoardRoom, event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (!isHousekeepingStatus(value) || value === room.housekeepingStatus) {
      return;
    }
    void this.store.changeStatus(room, value);
  }

  protected startNoteEdit(roomId: string): void {
    this.store.clearUpdateError();
    this.editingRoomId.set(roomId);
  }

  protected cancelNoteEdit(): void {
    this.editingRoomId.set(null);
  }

  protected async saveNote(room: HousekeepingBoardRoom, note: string): Promise<void> {
    const saved = await this.store.changeNote(room, note);
    if (saved) {
      this.editingRoomId.set(null);
    }
  }
}
