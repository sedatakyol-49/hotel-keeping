import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RoomsApi } from '../../core/api/rooms.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  HOUSEKEEPING_SUMMARY_FIELDS,
  type HousekeepingBoardFloor,
  type HousekeepingBoardRoom,
  type HousekeepingStatus,
  type HousekeepingSummary,
} from '../../core/models/room.model';

const EMPTY_SUMMARY: HousekeepingSummary = {
  clean: 0,
  dirty: 0,
  inspected: 0,
  outOfOrder: 0,
  total: 0,
};

/** Ekran okuyucuya bildirilecek son durum degisikligi. */
export interface HousekeepingAnnouncement {
  readonly kind: 'updated' | 'failed';
  readonly roomNumber: string;
  readonly status: HousekeepingStatus;
}

/** Iyimser guncellemede oda uzerine uygulanan degisiklikler. */
interface RoomPatch {
  readonly housekeepingStatus: HousekeepingStatus;
  readonly isOutOfOrder: boolean;
  readonly note: string | null;
}

/** Kat agacinda tek bir odayi degistirir (referans esitligi korunarak). */
export function applyRoomPatch(
  floors: readonly HousekeepingBoardFloor[],
  roomId: string,
  patch: RoomPatch,
): readonly HousekeepingBoardFloor[] {
  return floors.map((floor) =>
    floor.rooms.some((room) => room.id === roomId)
      ? {
          ...floor,
          rooms: floor.rooms.map((room) => (room.id === roomId ? { ...room, ...patch } : room)),
        }
      : floor,
  );
}

/** Sayaclari kat agacindan yeniden hesaplar (iyimser guncelleme sonrasi). */
export function computeSummary(floors: readonly HousekeepingBoardFloor[]): HousekeepingSummary {
  const counters = { clean: 0, dirty: 0, inspected: 0, outOfOrder: 0, total: 0 };
  for (const floor of floors) {
    for (const room of floor.rooms) {
      counters[HOUSEKEEPING_SUMMARY_FIELDS[room.housekeepingStatus]] += 1;
      counters.total += 1;
    }
  }
  return counters;
}

/**
 * Kat bazli housekeeping panosu (`GET /rooms/board`).
 *
 * Durum degisikligi **iyimser** uygulanir: kullanici geri bildirimi aninda
 * gorunur, `PATCH /rooms/{id}/housekeeping` basarisiz olursa kat agaci ve
 * sayaclar onceki haline geri alinir ve hata bildirilir.
 *
 * Bu ekranda **hicbir finansal alan yoktur** (mimari §7 — Housekeeping rolu
 * fiyat/ciro gormez); sozlesme geregi board yaniti da para alani icermez.
 */
@Injectable({ providedIn: 'root' })
export class HousekeepingStore {
  private readonly api = inject(RoomsApi);

  private readonly _floors = signal<readonly HousekeepingBoardFloor[]>([]);
  private readonly _summary = signal<HousekeepingSummary>(EMPTY_SUMMARY);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _updateError = signal<ApiError | null>(null);
  private readonly _statusFilter = signal<HousekeepingStatus | null>(null);
  private readonly _pendingRoomIds = signal<ReadonlySet<string>>(new Set<string>());
  private readonly _announcement = signal<HousekeepingAnnouncement | null>(null);

  readonly floors = this._floors.asReadonly();
  readonly summary = this._summary.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly updateError = this._updateError.asReadonly();
  readonly statusFilter = this._statusFilter.asReadonly();
  readonly announcement = this._announcement.asReadonly();

  /** Filtreye gore suzulmus katlar; bos kalan kat hic gosterilmez. */
  readonly visibleFloors = computed<readonly HousekeepingBoardFloor[]>(() => {
    const status = this._statusFilter();
    if (status === null) {
      return this._floors();
    }
    return this._floors()
      .map((floor) => ({
        ...floor,
        rooms: floor.rooms.filter((room) => room.housekeepingStatus === status),
      }))
      .filter((floor) => floor.rooms.length > 0);
  });

  readonly visibleRoomCount = computed(() =>
    this.visibleFloors().reduce((total, floor) => total + floor.rooms.length, 0),
  );

  readonly hasRooms = computed(() => this._summary().total > 0);
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._summary().total === 0,
  );

  isPending(roomId: string): boolean {
    return this._pendingRoomIds().has(roomId);
  }

  /** `GET /rooms/board`. */
  async load(): Promise<void> {
    this._loading.set(true);
    this._error.set(null);
    this._updateError.set(null);
    try {
      const board = await firstValueFrom(this.api.board());
      this._floors.set(board.floors);
      this._summary.set(board.summary);
    } catch (error: unknown) {
      this._floors.set([]);
      this._summary.set(EMPTY_SUMMARY);
      this._error.set(toApiError(error));
    } finally {
      this._loading.set(false);
    }
  }

  setStatusFilter(status: HousekeepingStatus | null): void {
    this._statusFilter.set(status);
  }

  clearUpdateError(): void {
    this._updateError.set(null);
  }

  /** Durum degisikligi — mevcut not korunur. */
  changeStatus(room: HousekeepingBoardRoom, status: HousekeepingStatus): Promise<boolean> {
    return this.patch(room, status, room.note ?? null);
  }

  /** Not duzenleme — mevcut durum korunur; bos metin notu temizler. */
  changeNote(room: HousekeepingBoardRoom, note: string): Promise<boolean> {
    const trimmed = note.trim();
    return this.patch(room, room.housekeepingStatus, trimmed === '' ? null : trimmed);
  }

  /**
   * Iyimser guncelleme + hata durumunda geri alma.
   * `isOutOfOrder` sozlesme geregi durumdan turetilir.
   */
  private async patch(
    room: HousekeepingBoardRoom,
    status: HousekeepingStatus,
    note: string | null,
  ): Promise<boolean> {
    const previousFloors = this._floors();
    const previousSummary = this._summary();

    this._updateError.set(null);
    this.applyOptimistic(room.id, {
      housekeepingStatus: status,
      isOutOfOrder: status === 'OutOfOrder',
      note,
    });
    this.markPending(room.id, true);

    try {
      const updated = await firstValueFrom(this.api.updateHousekeeping(room.id, { status, note }));
      // Sunucunun dondurdugu degerler esas alinir (tutarlilik kurallari orada uygulanir).
      this.applyOptimistic(room.id, {
        housekeepingStatus: updated.housekeepingStatus,
        isOutOfOrder: updated.isOutOfOrder,
        note: updated.note ?? null,
      });
      this._announcement.set({
        kind: 'updated',
        roomNumber: room.number,
        status: updated.housekeepingStatus,
      });
      return true;
    } catch (error: unknown) {
      // Geri alma: kat agaci ve sayaclar onceki haline doner.
      this._floors.set(previousFloors);
      this._summary.set(previousSummary);
      this._updateError.set(toApiError(error));
      this._announcement.set({
        kind: 'failed',
        roomNumber: room.number,
        status: room.housekeepingStatus,
      });
      return false;
    } finally {
      this.markPending(room.id, false);
    }
  }

  private applyOptimistic(roomId: string, patch: RoomPatch): void {
    const floors = applyRoomPatch(this._floors(), roomId, patch);
    this._floors.set(floors);
    this._summary.set(computeSummary(floors));
  }

  private markPending(roomId: string, pending: boolean): void {
    this._pendingRoomIds.update((current) => {
      const next = new Set(current);
      if (pending) {
        next.add(roomId);
      } else {
        next.delete(roomId);
      }
      return next;
    });
  }
}
