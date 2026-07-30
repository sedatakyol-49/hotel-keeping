import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { EmployeesApi } from '../../core/api/employees.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { EmployeeListQuery, EmployeeResponse } from '../../core/models/employee.model';
import { totalPages } from '../../core/models/paged-result.model';
import type { ApiError } from '../../core/models/problem-details.model';
import { DEFAULT_EMPLOYEE_LIST_QUERY, hasActiveEmployeeFilters } from './employee-list-query';

/**
 * Calisan listesi signal store'u.
 *
 * Sorgu (sayfa + filtreler) URL'den gelir; store yalnizca son calistirilan
 * sorguyu, sonucu ve yukleme/hata durumunu tutar. Ust uste gelen isteklerde
 * **yalnizca en son** yanit yazilir (`requestToken`), boylece hizli filtre
 * degisiminde eski sayfa ekrana dusmez.
 */
@Injectable({ providedIn: 'root' })
export class EmployeesStore {
  private readonly api = inject(EmployeesApi);

  private readonly _items = signal<readonly EmployeeResponse[]>([]);
  private readonly _query = signal<EmployeeListQuery>(DEFAULT_EMPLOYEE_LIST_QUERY);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _deletingId = signal<string | null>(null);
  private readonly _deleteError = signal<ApiError | null>(null);

  private requestToken = 0;

  readonly items = this._items.asReadonly();
  readonly query = this._query.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly deletingId = this._deletingId.asReadonly();
  readonly deleteError = this._deleteError.asReadonly();

  readonly page = computed(() => this._query().page);
  readonly pageSize = computed(() => this._query().pageSize);
  readonly totalPages = computed(() =>
    totalPages({ pageSize: this._query().pageSize, totalCount: this._totalCount() }),
  );
  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());
  readonly hasFilters = computed(() => hasActiveEmployeeFilters(this._query()));
  /** Yukleme bitti, hata yok ve sonuc bos. */
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );
  /** Gosterilen kayit araligi (`21 – 40`). */
  readonly rangeStart = computed(() =>
    this._totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly rangeEnd = computed(() =>
    Math.min(this._totalCount(), (this.page() - 1) * this.pageSize() + this._items().length),
  );

  /** `GET /employees` — sorgu URL'den geldigi icin parametre olarak alinir. */
  async load(query: EmployeeListQuery): Promise<void> {
    const token = ++this.requestToken;
    this._query.set(query);
    this._loading.set(true);
    this._error.set(null);
    this._deleteError.set(null);

    try {
      const result = await firstValueFrom(this.api.list(query));
      if (token !== this.requestToken) {
        return;
      }
      this._items.set(result.items);
      this._totalCount.set(result.totalCount);
      // Sayfa bilgisinde sunucunun yaniti esas alinir.
      this._query.update((current) => ({
        ...current,
        page: result.page,
        pageSize: result.pageSize,
      }));
    } catch (error: unknown) {
      if (token !== this.requestToken) {
        return;
      }
      this._items.set([]);
      this._totalCount.set(0);
      this._error.set(toApiError(error));
    } finally {
      if (token === this.requestToken) {
        this._loading.set(false);
      }
    }
  }

  /** Son sorguyu tekrar calistirir (yeniden dene / silme sonrasi). */
  async reload(): Promise<void> {
    await this.load(this._query());
  }

  /** `DELETE /employees/{id}` — soft-delete; hata cagirana iletilir. */
  async remove(id: string): Promise<ApiError | null> {
    this._deletingId.set(id);
    this._deleteError.set(null);
    try {
      await firstValueFrom(this.api.delete(id));
      // Son sayfadaki tek kayit silindiyse bir onceki sayfaya kayilir.
      const current = this._query();
      const remaining = this._items().length - 1;
      const nextPage = remaining === 0 && current.page > 1 ? current.page - 1 : current.page;
      await this.load({ ...current, page: nextPage });
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._deleteError.set(apiError);
      return apiError;
    } finally {
      this._deletingId.set(null);
    }
  }

  clearDeleteError(): void {
    this._deleteError.set(null);
  }
}
