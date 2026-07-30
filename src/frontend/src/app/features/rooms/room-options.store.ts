import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RoomsApi } from '../../core/api/rooms.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import type { RoomResponse } from '../../core/models/room.model';

/**
 * Secim listeleri icin oda envanteri (`GET /rooms?page=1&pageSize=200`).
 *
 * Neden ayri store: `RoomsStore` sayfali **liste ekraninin** durumunu tutar
 * (sorgu URL'den gelir). Rezervasyon filtresindeki `<select>` ise envanterin
 * tamamini ister ve sayfalamadan etkilenmemelidir — `EmployeeOptionsStore` ile
 * ayni yaklasim.
 *
 * Envanter sayfa tavanini asiyorsa `truncated` isaretlenir; ekran bunu
 * kullaniciya soyler (sessizce eksik liste gosterilmez).
 */
const OPTIONS_PAGE_SIZE = 200;

@Injectable({ providedIn: 'root' })
export class RoomOptionsStore {
  private readonly api = inject(RoomsApi);

  private readonly _items = signal<readonly RoomResponse[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _truncated = signal(false);
  private loaded = false;

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly truncated = this._truncated.asReadonly();

  /** Oda numarasina gore **dogal** sirali kopya ("2" < "10"). */
  readonly options = computed(() =>
    [...this._items()].sort((left, right) =>
      left.number.localeCompare(right.number, undefined, { numeric: true }),
    ),
  );

  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );

  /** `force` degilse ikinci kez ag istegi yapilmaz. */
  async load(force = false): Promise<void> {
    if (this.loaded && !force && this._error() === null) {
      return;
    }
    this._loading.set(true);
    this._error.set(null);
    try {
      const result = await firstValueFrom(
        this.api.list({ page: 1, pageSize: OPTIONS_PAGE_SIZE }),
      );
      this._items.set(result.items);
      this._truncated.set(result.totalCount > result.items.length);
      this.loaded = true;
    } catch (error: unknown) {
      this._items.set([]);
      this._error.set(toApiError(error));
    } finally {
      this._loading.set(false);
    }
  }

  findById(id: string): RoomResponse | null {
    return this._items().find((item) => item.id === id) ?? null;
  }

  invalidate(): void {
    this.loaded = false;
  }
}
