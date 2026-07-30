import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { TimeEntriesApi } from '../../core/api/time-entries.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import { totalPages } from '../../core/models/paged-result.model';
import type { ApiError } from '../../core/models/problem-details.model';
import type {
  TimeEntryListQuery,
  TimeEntryResponse,
  UpdateTimeEntryRequest,
} from '../../core/models/time-entry.model';
import { DEFAULT_TIME_ENTRY_LIST_QUERY, hasActiveTimeEntryFilters } from './time-entry-list-query';

/**
 * Zeiterfassung signal store'u.
 *
 * Liste sorgusu URL'den gelir. Giris/cikis panelinin ihtiyaci olan **acik
 * kayit** bilgisi ayri tutulur: secilen calisanin en son kaydi `pageSize=1`
 * ile okunur (sunucu `clockIn` azalan sirada dondurur), boylece
 * "acik kayit var mi" sorusu 409 almadan yanitlanir — sunucudan hata almak
 * normal akisin parcasi degildir.
 */
@Injectable({ providedIn: 'root' })
export class TimeTrackingStore {
  private readonly api = inject(TimeEntriesApi);

  private readonly _items = signal<readonly TimeEntryResponse[]>([]);
  private readonly _query = signal<TimeEntryListQuery>(DEFAULT_TIME_ENTRY_LIST_QUERY);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  private readonly _clockEmployeeId = signal<string | null>(null);
  private readonly _openEntry = signal<TimeEntryResponse | null>(null);
  private readonly _probing = signal(false);
  private readonly _clockBusy = signal(false);
  private readonly _clockError = signal<ApiError | null>(null);

  private readonly _savingId = signal<string | null>(null);
  private readonly _deletingId = signal<string | null>(null);
  private readonly _rowError = signal<ApiError | null>(null);

  private requestToken = 0;
  private probeToken = 0;

  readonly items = this._items.asReadonly();
  readonly query = this._query.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  readonly clockEmployeeId = this._clockEmployeeId.asReadonly();
  readonly openEntry = this._openEntry.asReadonly();
  readonly probing = this._probing.asReadonly();
  readonly clockBusy = this._clockBusy.asReadonly();
  readonly clockError = this._clockError.asReadonly();

  readonly savingId = this._savingId.asReadonly();
  readonly deletingId = this._deletingId.asReadonly();
  readonly rowError = this._rowError.asReadonly();

  readonly page = computed(() => this._query().page);
  readonly pageSize = computed(() => this._query().pageSize);
  readonly totalPages = computed(() =>
    totalPages({ pageSize: this._query().pageSize, totalCount: this._totalCount() }),
  );
  readonly hasPreviousPage = computed(() => this.page() > 1);
  readonly hasNextPage = computed(() => this.page() < this.totalPages());
  readonly hasFilters = computed(() => hasActiveTimeEntryFilters(this._query()));
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );
  readonly rangeStart = computed(() =>
    this._totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  readonly rangeEnd = computed(() =>
    Math.min(this._totalCount(), (this.page() - 1) * this.pageSize() + this._items().length),
  );

  /** Secili calisanin acik kaydi var mi (giris/cikis dugmesini secer). */
  readonly hasOpenEntry = computed(() => this._openEntry() !== null);
  /** Calisan secilmeden giris/cikis yapilamaz. */
  readonly canClock = computed(() => this._clockEmployeeId() !== null && !this._probing());

  /** `GET /time-entries` — sorgu URL'den geldigi icin parametre olarak alinir. */
  async load(query: TimeEntryListQuery): Promise<void> {
    const token = ++this.requestToken;
    this._query.set(query);
    this._loading.set(true);
    this._error.set(null);
    this._rowError.set(null);

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

  /**
   * Giris/cikis paneli icin calisan secimi. Secim degisince o calisanin **en
   * son** kaydi okunur; kayit aciksa yalnizca cikis, degilse yalnizca giris
   * dugmesi gosterilir.
   */
  async selectClockEmployee(employeeId: string | null): Promise<void> {
    this._clockEmployeeId.set(employeeId);
    this._clockError.set(null);
    this._openEntry.set(null);
    if (employeeId === null) {
      return;
    }
    await this.probeOpenEntry(employeeId);
  }

  /** Secili calisanin acik kaydini tazeler. */
  async refreshOpenEntry(): Promise<void> {
    const employeeId = this._clockEmployeeId();
    if (employeeId !== null) {
      await this.probeOpenEntry(employeeId);
    }
  }

  /** `POST /time-entries/clock-in` — `note` bos ise gonderilmez. */
  clockIn(note: string | null): Promise<ApiError | null> {
    const employeeId = this._clockEmployeeId();
    if (employeeId === null) {
      return Promise.resolve(null);
    }
    return this.runClockAction(() =>
      firstValueFrom(this.api.clockIn({ employeeId, note: note?.trim() || null })),
    );
  }

  /** `POST /time-entries/clock-out` — mola dakikasi ve not opsiyoneldir. */
  clockOut(breakMinutes: number | null, note: string | null): Promise<ApiError | null> {
    const employeeId = this._clockEmployeeId();
    if (employeeId === null) {
      return Promise.resolve(null);
    }
    return this.runClockAction(() =>
      firstValueFrom(
        this.api.clockOut({
          employeeId,
          breakMinutes: breakMinutes ?? null,
          note: note?.trim() || null,
        }),
      ),
    );
  }

  /** `PUT /time-entries/{id}` — manuel duzeltme; hata cagirana iletilir. */
  async update(id: string, request: UpdateTimeEntryRequest): Promise<ApiError | null> {
    this._savingId.set(id);
    this._rowError.set(null);
    try {
      await firstValueFrom(this.api.update(id, request));
      await Promise.all([this.reload(), this.refreshOpenEntry()]);
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._rowError.set(apiError);
      return apiError;
    } finally {
      this._savingId.set(null);
    }
  }

  /** `DELETE /time-entries/{id}`. */
  async remove(id: string): Promise<ApiError | null> {
    this._deletingId.set(id);
    this._rowError.set(null);
    try {
      await firstValueFrom(this.api.delete(id));
      // Son sayfadaki tek kayit silindiyse bir onceki sayfaya kayilir.
      const current = this._query();
      const remaining = this._items().length - 1;
      const nextPage = remaining === 0 && current.page > 1 ? current.page - 1 : current.page;
      await Promise.all([this.load({ ...current, page: nextPage }), this.refreshOpenEntry()]);
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._rowError.set(apiError);
      return apiError;
    } finally {
      this._deletingId.set(null);
    }
  }

  clearClockError(): void {
    this._clockError.set(null);
  }

  clearRowError(): void {
    this._rowError.set(null);
  }

  /** Giris/cikis ortak akisi: hata durum sinyaline yazilir, liste tazelenir. */
  private async runClockAction(action: () => Promise<TimeEntryResponse>): Promise<ApiError | null> {
    this._clockBusy.set(true);
    this._clockError.set(null);
    try {
      await action();
      await Promise.all([this.reload(), this.refreshOpenEntry()]);
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._clockError.set(apiError);
      // Sunucu ile istemcinin acik kayit gorusu ayrismis olabilir; tazelenir.
      await this.refreshOpenEntry();
      return apiError;
    } finally {
      this._clockBusy.set(false);
    }
  }

  /**
   * Calisanin en son kaydini okur (`pageSize=1`). Kayit acik degilse acik kayit
   * yok kabul edilir: sunucu `clockIn` azalan siralar ve acik kayit tanim
   * geregi tektir.
   */
  private async probeOpenEntry(employeeId: string): Promise<void> {
    const token = ++this.probeToken;
    this._probing.set(true);
    try {
      const result = await firstValueFrom(this.api.list({ page: 1, pageSize: 1, employeeId }));
      if (token !== this.probeToken) {
        return;
      }
      const latest = result.items[0];
      this._openEntry.set(latest?.isOpen ? latest : null);
    } catch (error: unknown) {
      if (token !== this.probeToken) {
        return;
      }
      this._openEntry.set(null);
      this._clockError.set(toApiError(error));
    } finally {
      if (token === this.probeToken) {
        this._probing.set(false);
      }
    }
  }
}
