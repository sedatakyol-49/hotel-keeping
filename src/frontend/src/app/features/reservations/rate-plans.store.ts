import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { RatePlansApi } from '../../core/api/rate-plans.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import type { RatePlanListQuery, RatePlanResponse } from '../../core/models/rate-plan.model';

/**
 * Fiyat plani signal store'u.
 *
 * Liste sayfalanmaz (sozlesme: duz dizi), bu yuzden tek bir dizi + yukleme/hata
 * durumu yeterlidir. Silme **hard delete**'tir ve plana bagli rezervasyon varsa
 * sunucu **409** doner — cozum plani pasife almaktir, bu yuzden hata mesaji
 * kullaniciyi `isActive: false` yoluna yonlendirir.
 */
@Injectable({ providedIn: 'root' })
export class RatePlansStore {
  private readonly api = inject(RatePlansApi);

  private readonly _items = signal<readonly RatePlanResponse[]>([]);
  private readonly _query = signal<RatePlanListQuery>({});
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _deletingId = signal<string | null>(null);
  private readonly _deleteError = signal<ApiError | null>(null);

  private requestToken = 0;

  readonly items = this._items.asReadonly();
  readonly query = this._query.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly deletingId = this._deletingId.asReadonly();
  readonly deleteError = this._deleteError.asReadonly();

  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );
  readonly hasFilters = computed(() => Boolean(this._query().roomTypeId || this._query().date));
  readonly activeCount = computed(() => this._items().filter((plan) => plan.isActive).length);

  /** `GET /rate-plans?roomTypeId=&date=`. */
  async load(query: RatePlanListQuery = {}): Promise<void> {
    const token = ++this.requestToken;
    this._query.set(query);
    this._loading.set(true);
    this._error.set(null);
    this._deleteError.set(null);

    try {
      const items = await firstValueFrom(this.api.list(query));
      if (token !== this.requestToken) {
        return;
      }
      this._items.set(items);
    } catch (error: unknown) {
      if (token !== this.requestToken) {
        return;
      }
      this._items.set([]);
      this._error.set(toApiError(error));
    } finally {
      if (token === this.requestToken) {
        this._loading.set(false);
      }
    }
  }

  async reload(): Promise<void> {
    await this.load(this._query());
  }

  /** `DELETE /rate-plans/{id}` — kullanilan plan silinemez (409). */
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

  clearDeleteError(): void {
    this._deleteError.set(null);
  }
}
