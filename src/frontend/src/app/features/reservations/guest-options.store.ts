import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { GuestsApi } from '../../core/api/guests.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { GuestResponse } from '../../core/models/guest.model';
import type { ApiError } from '../../core/models/problem-details.model';

/**
 * Secim listeleri icin misafir arama (`GET /guests?search=`).
 *
 * Neden ayri store: `GuestsStore` sayfali **liste ekraninin** durumunu tutar;
 * sihirbaz ve fatura formu ise "yaz-ara-sec" davranisi ister. Misafir sayisi
 * (otel buyuklugune gore) binlerce olabilecegi icin tum kadroyu onbellege alan
 * bir `options` yaklasimi (bkz. `EmployeeOptionsStore`) burada **yanlis**
 * olurdu: arama sunucuda yapilir, istemci yalnizca ilk sayfayi gosterir.
 *
 * Sozlesme notu: misafirde **benzersizlik kurali yoktur** — ayni ad/e-posta ile
 * birden cok kayit mesrudur. Bu yuzden arama sonucu sessizce tek bir kayda
 * baglanmaz; kullanici hangi misafiri sectigini acikca gorur.
 */
const SEARCH_PAGE_SIZE = 20;

@Injectable({ providedIn: 'root' })
export class GuestOptionsStore {
  private readonly api = inject(GuestsApi);

  private readonly _items = signal<readonly GuestResponse[]>([]);
  private readonly _search = signal('');
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  private requestToken = 0;

  readonly items = this._items.asReadonly();
  readonly search = this._search.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  /** Sunucu ilk sayfayi dondurdugu icin sonuc kirpilmis olabilir. */
  readonly truncated = computed(() => this._totalCount() > this._items().length);
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._items().length === 0,
  );

  /** `GET /guests?page=1&pageSize=20&search=`. */
  async load(search: string | null = null): Promise<void> {
    const token = ++this.requestToken;
    const trimmed = search?.trim() ?? '';
    this._search.set(trimmed);
    this._loading.set(true);
    this._error.set(null);

    try {
      const result = await firstValueFrom(
        this.api.list({ page: 1, pageSize: SEARCH_PAGE_SIZE, search: trimmed || null }),
      );
      if (token !== this.requestToken) {
        return;
      }
      this._items.set(result.items);
      this._totalCount.set(result.totalCount);
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

  findById(id: string): GuestResponse | null {
    return this._items().find((item) => item.id === id) ?? null;
  }

  /** Yeni olusturulan misafiri listenin basina koyar (sihirbazda hemen secilebilsin). */
  prepend(guest: GuestResponse): void {
    this._items.update((items) => [guest, ...items.filter((item) => item.id !== guest.id)]);
    this._totalCount.update((count) => count + 1);
  }
}
