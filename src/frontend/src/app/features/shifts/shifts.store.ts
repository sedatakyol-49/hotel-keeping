import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ShiftsApi } from '../../core/api/shifts.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import type {
  ShiftPlanDay,
  ShiftPlanEmployee,
  ShiftPlanResponse,
  ShiftResponse,
  ShiftWriteRequest,
} from '../../core/models/shift.model';

/** Izgara hucresinin anahtari (`employeeId` + gun). */
export function cellKey(employeeId: string, date: string): string {
  return `${employeeId}|${date}`;
}

/**
 * Haftalik vardiya plani signal store'u (`GET /shifts?week=YYYY-Www`).
 *
 * Hafta URL'den gelir; store yalnizca son yuklenen plani, yukleme/hata
 * durumunu ve hucre yazma durumunu tutar. Ust uste gelen isteklerde yalnizca
 * **en son** yanit yazilir (`requestToken`) — hizli hafta gezinmesinde onceki
 * haftanin plani ekrana dusmez.
 *
 * Yazma islemleri (`POST`/`PUT`/`DELETE /shifts`) sonrasinda plan yeniden
 * yuklenir: `(employeeId, date)` benzersizligi ve calisan adi sunucu
 * tarafindan uretilir, istemci bunlari kendisi turetmez.
 */
@Injectable({ providedIn: 'root' })
export class ShiftsStore {
  private readonly api = inject(ShiftsApi);

  private readonly _plan = signal<ShiftPlanResponse | null>(null);
  private readonly _week = signal<string | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _saving = signal(false);
  private readonly _writeError = signal<ApiError | null>(null);

  private requestToken = 0;

  readonly plan = this._plan.asReadonly();
  readonly week = this._week.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly writeError = this._writeError.asReadonly();

  readonly days = computed<readonly ShiftPlanDay[]>(() => this._plan()?.days ?? []);
  readonly employees = computed<readonly ShiftPlanEmployee[]>(() => this._plan()?.employees ?? []);
  readonly from = computed(() => this._plan()?.from ?? null);
  readonly to = computed(() => this._plan()?.to ?? null);

  /** `employeeId|date` -> vardiya. Izgara hucreleri bu sozlukten okur. */
  readonly shiftsByCell = computed<ReadonlyMap<string, ShiftResponse>>(() => {
    const map = new Map<string, ShiftResponse>();
    for (const day of this.days()) {
      for (const shift of day.shifts) {
        map.set(cellKey(shift.employeeId, day.date), shift);
      }
    }
    return map;
  });

  readonly shiftCount = computed(() =>
    this.days().reduce((total, day) => total + day.shifts.length, 0),
  );

  /** Kadro var ama haftada hic vardiya yok (bos hafta durumu). */
  readonly isWeekEmpty = computed(
    () =>
      !this._loading() &&
      this._error() === null &&
      this.employees().length > 0 &&
      this.shiftCount() === 0,
  );

  /** Otelde hic (aktif) calisan yok — izgara hic cizilemez. */
  readonly hasNoEmployees = computed(
    () =>
      !this._loading() &&
      this._error() === null &&
      this._plan() !== null &&
      this.employees().length === 0,
  );

  shiftFor(employeeId: string, date: string): ShiftResponse | null {
    return this.shiftsByCell().get(cellKey(employeeId, date)) ?? null;
  }

  /** `GET /shifts?week=` — hafta URL'den geldigi icin parametre olarak alinir. */
  async load(week: string): Promise<void> {
    const token = ++this.requestToken;
    this._week.set(week);
    this._loading.set(true);
    this._error.set(null);
    this._writeError.set(null);

    try {
      const plan = await firstValueFrom(this.api.planByWeek(week));
      if (token !== this.requestToken) {
        return;
      }
      this._plan.set(plan);
    } catch (error: unknown) {
      if (token !== this.requestToken) {
        return;
      }
      this._plan.set(null);
      this._error.set(toApiError(error));
    } finally {
      if (token === this.requestToken) {
        this._loading.set(false);
      }
    }
  }

  async reload(): Promise<void> {
    const week = this._week();
    if (week !== null) {
      await this.load(week);
    }
  }

  /**
   * Hucre atama/degistirme. `existingId` verilirse `PUT`, aksi halde `POST`.
   * Ayni gune ikinci vardiya sunucuda **409** uretir; hata cagirana dondurulur.
   */
  async save(request: ShiftWriteRequest, existingId: string | null): Promise<ApiError | null> {
    this._saving.set(true);
    this._writeError.set(null);
    try {
      if (existingId === null) {
        await firstValueFrom(this.api.create(request));
      } else {
        await firstValueFrom(this.api.update(existingId, request));
      }
      await this.reload();
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._writeError.set(apiError);
      return apiError;
    } finally {
      this._saving.set(false);
    }
  }

  /** `DELETE /shifts/{id}` — hucreyi bosaltir. */
  async remove(id: string): Promise<ApiError | null> {
    this._saving.set(true);
    this._writeError.set(null);
    try {
      await firstValueFrom(this.api.delete(id));
      await this.reload();
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._writeError.set(apiError);
      return apiError;
    } finally {
      this._saving.set(false);
    }
  }

  clearWriteError(): void {
    this._writeError.set(null);
  }
}
