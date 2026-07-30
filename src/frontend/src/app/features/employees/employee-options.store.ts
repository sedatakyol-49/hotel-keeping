import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { EmployeesApi } from '../../core/api/employees.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { EmployeeResponse } from '../../core/models/employee.model';
import type { ApiError } from '../../core/models/problem-details.model';

/**
 * Secim listeleri icin calisan kadrosu (`GET /employees`).
 *
 * Neden ayri bir store: `EmployeesStore` sayfalı **liste ekraninin** durumunu
 * tutar (sorgu URL'den gelir, sayfa degisince icerik degisir). Izin, zaman ve
 * vardiya ekranlarindaki `<select>`'ler ise kadronun tamamini ister ve
 * sayfalamadan etkilenmemelidir. Bu store bir kez yuklenir ve tum HR
 * ekranlarinda paylasilir.
 *
 * Sozlesme sinirlari: sayfa boyutu tavani 200'dur ve `includeTerminated`
 * varsayilani `false` — isten ayrilmislar secim listesinde gorunmez.
 */
const OPTIONS_PAGE_SIZE = 200;

@Injectable({ providedIn: 'root' })
export class EmployeeOptionsStore {
  private readonly api = inject(EmployeesApi);

  private readonly _items = signal<readonly EmployeeResponse[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  /** Kadro sayfa tavanini asiyorsa ekran bunu kullaniciya bildirir. */
  private readonly _truncated = signal(false);
  private loaded = false;

  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly truncated = this._truncated.asReadonly();

  /** Ada gore siralanmis kopya (sunucu da soyada gore sirali doner). */
  readonly options = computed(() =>
    [...this._items()].sort((left, right) => left.fullName.localeCompare(right.fullName)),
  );

  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );

  /** `GET /employees?page=1&pageSize=200`. `force` degilse tekrar istek yapilmaz. */
  async load(force = false): Promise<void> {
    if (this.loaded && !force && this._error() === null) {
      return;
    }
    this._loading.set(true);
    this._error.set(null);
    try {
      const result = await firstValueFrom(
        this.api.list({ page: 1, pageSize: OPTIONS_PAGE_SIZE, includeTerminated: false }),
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

  findById(id: string): EmployeeResponse | null {
    return this._items().find((item) => item.id === id) ?? null;
  }

  /** Yazma islemi sonrasi listenin bayatlamasini engeller. */
  invalidate(): void {
    this.loaded = false;
  }
}
