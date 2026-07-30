import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ReservationsApi } from '../../core/api/reservations.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  canCancel,
  canCheckIn,
  canCheckOut,
  canMarkNoShow,
  type FolioResponse,
  type ReservationResponse,
} from '../../core/models/reservation.model';
import type { ReservationAction } from './reservations.store';

/**
 * Rezervasyon detayi + folio signal store'u.
 *
 * Detay ve folio **paralel** yuklenir (iki bagimsiz uc); folio hatasi detayi
 * gizlemez — acik hesap gorunmese de check-in/check-out yapilabilmelidir.
 *
 * Durum aksiyonlarinin **gorunurlugu** durum makinesinden turetilir
 * (`canCheckIn` vb.): gecersiz gecisin dugmesi hic render edilmez. Sunucu yine
 * 409 dondurur, ama kullaniciya yasak yolu gostermek yanlis olurdu.
 */
@Injectable({ providedIn: 'root' })
export class ReservationDetailStore {
  private readonly api = inject(ReservationsApi);

  private readonly _reservation = signal<ReservationResponse | null>(null);
  private readonly _folio = signal<FolioResponse | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _folioError = signal<ApiError | null>(null);
  private readonly _pendingAction = signal<ReservationAction | null>(null);
  private readonly _actionError = signal<ApiError | null>(null);
  /** Basarili aksiyondan sonra ekran okuyucuya bildirilecek olay. */
  private readonly _lastAction = signal<ReservationAction | null>(null);

  private requestToken = 0;

  readonly reservation = this._reservation.asReadonly();
  readonly folio = this._folio.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly folioError = this._folioError.asReadonly();
  readonly pendingAction = this._pendingAction.asReadonly();
  readonly actionError = this._actionError.asReadonly();
  readonly lastAction = this._lastAction.asReadonly();

  readonly status = computed(() => this._reservation()?.status ?? null);

  readonly canCheckIn = computed(() => {
    const status = this.status();
    return status !== null && canCheckIn(status);
  });
  readonly canCheckOut = computed(() => {
    const status = this.status();
    return status !== null && canCheckOut(status);
  });
  readonly canCancel = computed(() => {
    const status = this.status();
    return status !== null && canCancel(status);
  });
  readonly canMarkNoShow = computed(() => {
    const status = this.status();
    return status !== null && canMarkNoShow(status);
  });

  /** Folio satiri var mi (yoksa "henuz masraf yok" metni gosterilir). */
  readonly hasFolioLines = computed(() => (this._folio()?.lines.length ?? 0) > 0);

  /** Detay + folio paralel yuklenir. */
  async load(id: string): Promise<void> {
    const token = ++this.requestToken;
    this._loading.set(true);
    this._error.set(null);
    this._folioError.set(null);
    this._actionError.set(null);
    this._lastAction.set(null);

    const [reservation, folio] = await Promise.allSettled([
      firstValueFrom(this.api.getById(id)),
      firstValueFrom(this.api.folio(id)),
    ]);

    if (token !== this.requestToken) {
      return;
    }

    if (reservation.status === 'fulfilled') {
      this._reservation.set(reservation.value);
    } else {
      this._reservation.set(null);
      this._error.set(toApiError(reservation.reason));
    }

    if (folio.status === 'fulfilled') {
      this._folio.set(folio.value);
    } else {
      this._folio.set(null);
      this._folioError.set(toApiError(folio.reason));
    }

    this._loading.set(false);
  }

  async reload(): Promise<void> {
    const id = this._reservation()?.id;
    if (id) {
      await this.load(id);
    }
  }

  /**
   * Durum aksiyonu.
   *
   * Basarili olursa **hem** rezervasyon **hem** folio yeniden yuklenir: tutar
   * degismese de folio `isClosed` gibi alanlari degisebilir ve check-out odayi
   * `Dirty` yapar (housekeeping ekrani ayri store'dan besleniyor, orasi kendi
   * yuklemesinde guncel veriyi gorur).
   */
  async run(action: ReservationAction, reason: string | null = null): Promise<ApiError | null> {
    const current = this._reservation();
    if (!current) {
      return null;
    }

    this._pendingAction.set(action);
    this._actionError.set(null);

    try {
      const updated = await firstValueFrom(this.execute(action, current.id, reason));
      this._reservation.set(updated);
      this._lastAction.set(action);
      await this.loadFolio(current.id);
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._actionError.set(apiError);
      return apiError;
    } finally {
      this._pendingAction.set(null);
    }
  }

  clearActionError(): void {
    this._actionError.set(null);
  }

  clearLastAction(): void {
    this._lastAction.set(null);
  }

  private async loadFolio(id: string): Promise<void> {
    try {
      this._folio.set(await firstValueFrom(this.api.folio(id)));
      this._folioError.set(null);
    } catch (error: unknown) {
      this._folioError.set(toApiError(error));
    }
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
