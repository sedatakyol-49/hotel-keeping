import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ReservationsApi } from '../../core/api/reservations.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type {
  OccupancyQuery,
  OccupancySummaryResponse,
} from '../../core/models/availability.model';
import { OCCUPANCY_MAX_DAYS } from '../../core/models/availability.model';
import type { ApiError } from '../../core/models/problem-details.model';
import { buildOccupancyRows, type OccupancyRowView } from './occupancy-grid';

const EMPTY_SUMMARY: OccupancySummaryResponse = {
  roomCount: 0,
  days: 0,
  roomNights: 0,
  occupiedRoomNights: 0,
  occupancyRate: 0,
};

/**
 * Doluluk plani signal store'u (`GET /occupancy`).
 *
 * Tarih araligi URL'den gelir; store son calistirilan araligi, izgara
 * gorunumunu ve yukleme/hata durumunu tutar. Ust uste gelen isteklerde
 * **yalnizca en son** yanit yazilir (`requestToken`).
 *
 * Seyrek hucreler burada **bir kez** segmentlere cevrilir (`buildOccupancyRows`)
 * ve sablon hazir gorunumu okur — her change detection'da yeniden hesaplanmaz.
 */
@Injectable({ providedIn: 'root' })
export class OccupancyStore {
  private readonly api = inject(ReservationsApi);

  private readonly _days = signal<readonly string[]>([]);
  private readonly _rows = signal<readonly OccupancyRowView[]>([]);
  private readonly _summary = signal<OccupancySummaryResponse>(EMPTY_SUMMARY);
  private readonly _query = signal<OccupancyQuery | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  private requestToken = 0;

  readonly days = this._days.asReadonly();
  readonly rows = this._rows.asReadonly();
  readonly summary = this._summary.asReadonly();
  readonly query = this._query.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  /** Izgara kolon sayisi (`days.length`) — `colgroup` ile birebir ayni. */
  readonly dayCount = computed(() => this._days().length);
  readonly hasRooms = computed(() => this._rows().length > 0);
  readonly isEmpty = computed(
    () => !this._loading() && this._error() === null && this._rows().length === 0,
  );
  /** Pencerede hic konaklama yok mu (izgara yine cizilir). */
  readonly hasNoStays = computed(() => this._rows().every((row) => row.barCount === 0));
  readonly stayCount = computed(() =>
    this._rows().reduce((total, row) => total + row.barCount, 0),
  );

  /**
   * `GET /occupancy?from=&to=`.
   *
   * Sunucu tavani (92 gun) **istemcide** de dogrulanir: asan aralikta istek hic
   * gonderilmez ve alan hatasi olarak isaretlenir. Normal akista `parseOccupancyRange`
   * araligi zaten kirptigi icin bu dal yalnizca son savunma hattidir.
   */
  async load(query: OccupancyQuery): Promise<void> {
    const token = ++this.requestToken;
    this._query.set(query);

    const nights = nightsOf(query);
    if (nights === null || nights < 1 || nights > OCCUPANCY_MAX_DAYS) {
      this._days.set([]);
      this._rows.set([]);
      this._summary.set(EMPTY_SUMMARY);
      this._error.set({
        status: 400,
        messageKey: 'occupancy.rangeTooLong',
        fieldErrors: { To: ['occupancy.rangeTooLong'] },
      });
      this._loading.set(false);
      return;
    }

    this._loading.set(true);
    this._error.set(null);

    try {
      const response = await firstValueFrom(this.api.occupancy(query));
      if (token !== this.requestToken) {
        return;
      }
      this._days.set(response.days);
      this._rows.set(buildOccupancyRows(response.days, response.rooms));
      this._summary.set(response.summary);
    } catch (error: unknown) {
      if (token !== this.requestToken) {
        return;
      }
      this._days.set([]);
      this._rows.set([]);
      this._summary.set(EMPTY_SUMMARY);
      this._error.set(toApiError(error));
    } finally {
      if (token === this.requestToken) {
        this._loading.set(false);
      }
    }
  }

  async reload(): Promise<void> {
    const query = this._query();
    if (query) {
      await this.load(query);
    }
  }
}

function nightsOf(query: OccupancyQuery): number | null {
  const start = Date.parse(`${query.from}T00:00:00Z`);
  const end = Date.parse(`${query.to}T00:00:00Z`);
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return null;
  }
  return Math.round((end - start) / 86_400_000);
}
