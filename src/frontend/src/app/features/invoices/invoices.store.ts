import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { InvoicesApi } from '../../core/api/invoices.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { InvoiceListQuery, InvoiceResponse } from '../../core/models/invoice.model';
import { totalPages } from '../../core/models/paged-result.model';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  DEFAULT_INVOICE_LIST_QUERY,
  hasActiveInvoiceFilters,
  hasDateFilter,
} from './invoice-list-query';

/**
 * Fatura listesi signal store'u (`GET /invoices`).
 *
 * Sorgu URL'den gelir; store son sorguyu, sonucu ve yukleme/hata durumunu
 * tutar. Ust uste gelen isteklerde **yalnizca en son** yanit yazilir.
 *
 * Siralama sunucuya aittir (`COALESCE(issuedAt, createdAt) DESC`) ve istemci
 * yeniden siralamaz — taslaklarin listede nerede duracagi sunucu kararidir.
 */
@Injectable({ providedIn: 'root' })
export class InvoicesStore {
  private readonly api = inject(InvoicesApi);

  private readonly _items = signal<readonly InvoiceResponse[]>([]);
  private readonly _query = signal<InvoiceListQuery>(DEFAULT_INVOICE_LIST_QUERY);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  private requestToken = 0;

  readonly items = this._items.asReadonly();
  readonly query = this._query.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly page = computed(() => this._query().page);
  readonly pageSize = computed(() => this._query().pageSize);
  readonly totalPages = computed(() =>
    totalPages({ pageSize: this._query().pageSize, totalCount: this._totalCount() }),
  );
  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());
  readonly hasFilters = computed(() => hasActiveInvoiceFilters(this._query()));
  /** Tarih filtresi aktifse taslaklar listelenmez — ekran bunu soyler. */
  readonly draftsHidden = computed(() => hasDateFilter(this._query()));
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );
  readonly rangeStart = computed(() =>
    this._totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly rangeEnd = computed(() =>
    Math.min(this._totalCount(), (this.page() - 1) * this.pageSize() + this._items().length),
  );

  async load(query: InvoiceListQuery): Promise<void> {
    const token = ++this.requestToken;
    this._query.set(query);
    this._loading.set(true);
    this._error.set(null);

    try {
      const result = await firstValueFrom(this.api.list(query));
      if (token !== this.requestToken) {
        return;
      }
      this._items.set(result.items);
      this._totalCount.set(result.totalCount);
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

  async reload(): Promise<void> {
    await this.load(this._query());
  }
}
