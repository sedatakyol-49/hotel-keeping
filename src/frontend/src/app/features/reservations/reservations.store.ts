import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ReservationsApi } from '../../core/api/reservations.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import { totalPages } from '../../core/models/paged-result.model';
import type { ApiError } from '../../core/models/problem-details.model';
import type {
  ReservationListQuery,
  ReservationResponse,
} from '../../core/models/reservation.model';
import {
  DEFAULT_RESERVATION_LIST_QUERY,
  hasActiveReservationFilters,
} from './reservation-list-query';

/** Rezervasyon uzerinde yurutulen durum aksiyonu. */
export type ReservationAction = 'check-in' | 'check-out' | 'cancel' | 'no-show';

/** Aksiyon -> hedef durum (mesaj/onay metni ve gorunurluk kurallari icin). */
export const RESERVATION_ACTION_TARGETS = {
  'check-in': 'CheckedIn',
  'check-out': 'CheckedOut',
  cancel: 'Cancelled',
  'no-show': 'NoShow',
} as const;

/**
 * Rezervasyon listesi signal store'u.
 *
 * Sorgu (sayfa + filtreler) URL'den gelir; store yalnizca son calistirilan
 * sorguyu, sonucu ve yukleme/hata durumunu tutar. Ust uste gelen isteklerde
 * **yalnizca en son** yanit yazilir (`requestToken`).
 *
 * Durum aksiyonlari (check-in/check-out/cancel/no-show) listeden de
 * yurutulebilir; basarili aksiyondan sonra liste yenilenir cunku aksiyon
 * yalnizca durumu degil oda temizlik durumunu da etkiler (check-out → `Dirty`).
 */
@Injectable({ providedIn: 'root' })
export class ReservationsStore {
  private readonly api = inject(ReservationsApi);

  private readonly _items = signal<readonly ReservationResponse[]>([]);
  private readonly _query = signal<ReservationListQuery>(DEFAULT_RESERVATION_LIST_QUERY);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  private readonly _pendingActionId = signal<string | null>(null);
  private readonly _actionError = signal<ApiError | null>(null);

  private requestToken = 0;

  readonly items = this._items.asReadonly();
  readonly query = this._query.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly pendingActionId = this._pendingActionId.asReadonly();
  readonly actionError = this._actionError.asReadonly();

  readonly page = computed(() => this._query().page);
  readonly pageSize = computed(() => this._query().pageSize);
  readonly totalPages = computed(() =>
    totalPages({ pageSize: this._query().pageSize, totalCount: this._totalCount() }),
  );
  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());
  readonly hasFilters = computed(() => hasActiveReservationFilters(this._query()));
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );
  readonly rangeStart = computed(() =>
    this._totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly rangeEnd = computed(() =>
    Math.min(this._totalCount(), (this.page() - 1) * this.pageSize() + this._items().length),
  );

  /** `GET /reservations` — sorgu URL'den geldigi icin parametre olarak alinir. */
  async load(query: ReservationListQuery): Promise<void> {
    const token = ++this.requestToken;
    this._query.set(query);
    this._loading.set(true);
    this._error.set(null);
    this._actionError.set(null);

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

  /** Son sorguyu tekrar calistirir (yeniden dene / aksiyon sonrasi). */
  async reload(): Promise<void> {
    await this.load(this._query());
  }

  /**
   * Durum aksiyonu. Basarili olursa liste yenilenir; hata cagirana dondurulur
   * (409: gecersiz gecis veya erken check-in — sunucu mesaji hangi gecisin
   * denendigini soyler).
   */
  async run(
    action: ReservationAction,
    id: string,
    reason: string | null = null,
  ): Promise<ApiError | null> {
    this._pendingActionId.set(id);
    this._actionError.set(null);

    try {
      await firstValueFrom(this.execute(action, id, reason));
      await this.reload();
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._actionError.set(apiError);
      return apiError;
    } finally {
      this._pendingActionId.set(null);
    }
  }

  clearActionError(): void {
    this._actionError.set(null);
  }

  private execute(action: ReservationAction, id: string, reason: string | null) {
    switch (action) {
      case 'check-in':
        return this.api.checkIn(id);
      case 'check-out':
        return this.api.checkOut(id);
      case 'no-show':
        return this.api.noShow(id);
      case 'cancel':
        return this.api.cancel(id, reason ? { reason } : {});
    }
  }
}
