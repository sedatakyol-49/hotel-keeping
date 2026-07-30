import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { VacationsApi } from '../../core/api/vacations.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import { totalPages } from '../../core/models/paged-result.model';
import type { ApiError } from '../../core/models/problem-details.model';
import type {
  VacationBalanceQuery,
  VacationBalanceResponse,
  VacationListQuery,
  VacationRequestResponse,
} from '../../core/models/vacation.model';
import { DEFAULT_VACATION_LIST_QUERY, hasActiveVacationFilters } from './vacation-list-query';

/** Satir uzerinde yurutulen karar aksiyonu. */
export type VacationDecision = 'approve' | 'reject' | 'cancel';

/**
 * Izin talepleri signal store'u.
 *
 * Sorgu (sayfa + filtreler) URL'den gelir; store yalnizca son calistirilan
 * sorguyu, sonucu ve yukleme/hata durumunu tutar. Ust uste gelen isteklerde
 * **yalnizca en son** yanit yazilir (`requestToken`).
 *
 * Bakiye paneli ayni store'da durur: onay/ret/iptal bakiyeyi degistirdigi icin
 * (onay duser, iptal geri verir) her basarili karardan sonra **hem liste hem
 * bakiye** yeniden yuklenir — ekranda bayat kalan sayi olmaz.
 */
@Injectable({ providedIn: 'root' })
export class VacationsStore {
  private readonly api = inject(VacationsApi);

  private readonly _items = signal<readonly VacationRequestResponse[]>([]);
  private readonly _query = signal<VacationListQuery>(DEFAULT_VACATION_LIST_QUERY);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  private readonly _pendingActionId = signal<string | null>(null);
  private readonly _actionError = signal<ApiError | null>(null);

  private readonly _balances = signal<readonly VacationBalanceResponse[]>([]);
  private readonly _balanceQuery = signal<VacationBalanceQuery>({});
  private readonly _balancesLoading = signal(false);
  private readonly _balancesError = signal<ApiError | null>(null);

  private requestToken = 0;
  private balanceToken = 0;

  readonly items = this._items.asReadonly();
  readonly query = this._query.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly pendingActionId = this._pendingActionId.asReadonly();
  readonly actionError = this._actionError.asReadonly();
  readonly balances = this._balances.asReadonly();
  readonly balancesLoading = this._balancesLoading.asReadonly();
  readonly balancesError = this._balancesError.asReadonly();

  readonly page = computed(() => this._query().page);
  readonly pageSize = computed(() => this._query().pageSize);
  readonly totalPages = computed(() =>
    totalPages({ pageSize: this._query().pageSize, totalCount: this._totalCount() }),
  );
  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());
  readonly hasFilters = computed(() => hasActiveVacationFilters(this._query()));
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );
  readonly rangeStart = computed(() =>
    this._totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly rangeEnd = computed(() =>
    Math.min(this._totalCount(), (this.page() - 1) * this.pageSize() + this._items().length),
  );

  /** Bakiye satirlarinin yili (hepsi ayni yila aittir; sunucu doldurur). */
  readonly balanceYear = computed(
    () => this._balances()[0]?.year ?? this._balanceQuery().year ?? null,
  );
  readonly balancesEmpty = computed(
    () =>
      !this._balancesLoading() && this._balancesError() === null && this._balances().length === 0,
  );

  /** `GET /vacations` — sorgu URL'den geldigi icin parametre olarak alinir. */
  async load(query: VacationListQuery): Promise<void> {
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

  /** Son sorguyu tekrar calistirir (yeniden dene / karar sonrasi). */
  async reload(): Promise<void> {
    await this.load(this._query());
  }

  /**
   * `GET /vacations/balances?employeeId=&year=` — duz dizi.
   * `year` bos gonderilirse sunucunun gecerli yili kullanilir.
   */
  async loadBalances(query: VacationBalanceQuery): Promise<void> {
    const token = ++this.balanceToken;
    this._balanceQuery.set(query);
    this._balancesLoading.set(true);
    this._balancesError.set(null);

    try {
      const balances = await firstValueFrom(this.api.balances(query));
      if (token !== this.balanceToken) {
        return;
      }
      this._balances.set(balances);
    } catch (error: unknown) {
      if (token !== this.balanceToken) {
        return;
      }
      this._balances.set([]);
      this._balancesError.set(toApiError(error));
    } finally {
      if (token === this.balanceToken) {
        this._balancesLoading.set(false);
      }
    }
  }

  reloadBalances(): Promise<void> {
    return this.loadBalances(this._balanceQuery());
  }

  /**
   * Karar aksiyonu (`approve` / `reject` / `cancel`).
   *
   * Basarili olursa liste **ve** bakiye yenilenir; hata cagirana dondurulur
   * (409: karara baglanmis talep, 403: baskasinin talebini iptal etme).
   */
  async decide(
    decision: VacationDecision,
    id: string,
    decisionNote: string | null = null,
  ): Promise<ApiError | null> {
    this._pendingActionId.set(id);
    this._actionError.set(null);
    const body = decisionNote ? { decisionNote } : {};

    try {
      if (decision === 'approve') {
        await firstValueFrom(this.api.approve(id, body));
      } else if (decision === 'reject') {
        await firstValueFrom(this.api.reject(id, body));
      } else {
        await firstValueFrom(this.api.cancel(id, body));
      }
      await Promise.all([this.reload(), this.reloadBalances()]);
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
}
