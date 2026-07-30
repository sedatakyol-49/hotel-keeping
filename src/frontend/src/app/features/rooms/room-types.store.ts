import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RoomTypesApi } from '../../core/api/room-types.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import type { RoomTypeResponse } from '../../core/models/room-type.model';

/**
 * Oda tipi signal store'u. Liste sayfalanmaz (sozlesme: duz dizi), bu yuzden
 * tek bir dizi + yukleme/hata durumu yeterlidir. Oda listesi filtresi de bu
 * store'u okur — veri iki yerde kopyalanmaz.
 */
@Injectable({ providedIn: 'root' })
export class RoomTypesStore {
  private readonly api = inject(RoomTypesApi);

  private readonly _items = signal<readonly RoomTypeResponse[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _deletingId = signal<string | null>(null);
  private readonly _deleteError = signal<ApiError | null>(null);
  private loaded = false;

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly deletingId = this._deletingId.asReadonly();
  readonly deleteError = this._deleteError.asReadonly();

  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );
  /** Filtre/secim listeleri icin koda gore siralanmis kopya. */
  readonly options = computed(() =>
    [...this._items()].sort((left, right) => left.code.localeCompare(right.code)),
  );

  /** `GET /room-types`. `force` degilse ikinci kez ag istegi yapilmaz. */
  async load(force = false): Promise<void> {
    if (this.loaded && !force && this._error() === null) {
      return;
    }
    this._loading.set(true);
    this._error.set(null);
    try {
      const items = await firstValueFrom(this.api.list());
      this._items.set(items);
      this.loaded = true;
    } catch (error: unknown) {
      this._items.set([]);
      this._error.set(toApiError(error));
    } finally {
      this._loading.set(false);
    }
  }

  /** `DELETE /room-types/{id}` — bagli oda varsa backend 409 doner. */
  async remove(id: string): Promise<ApiError | null> {
    this._deletingId.set(id);
    this._deleteError.set(null);
    try {
      await firstValueFrom(this.api.delete(id));
      this._items.update((items) => items.filter((item) => item.id !== id));
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._deleteError.set(apiError);
      return apiError;
    } finally {
      this._deletingId.set(null);
    }
  }

  /** Yazma islemi sonrasi listenin bayatlamasini engeller. */
  invalidate(): void {
    this.loaded = false;
  }

  clearDeleteError(): void {
    this._deleteError.set(null);
  }
}
