/**
 * Vardiya (Shift) modulu tipleri (api-contracts.md → "Vardiya (Shift)").
 *
 * `GET /shifts?week=YYYY-Www` gun bazli bir plan dondurur; ayrica yanitta
 * aktif otelin **calisan listesi** (`employees`) gelir — izgaranin satirlari
 * bundan kurulur, ayri bir `GET /employees` istegi gerekmez.
 */

/** Vardiya tipi — backend enum'unun **adi** tasinir. */
export const SHIFT_TYPES = ['Morning', 'Evening', 'Night', 'Off'] as const;

export type ShiftType = (typeof SHIFT_TYPES)[number];

export function isShiftType(value: unknown): value is ShiftType {
  return typeof value === 'string' && (SHIFT_TYPES as readonly string[]).includes(value);
}

/** Vardiya tipi -> i18n anahtari (DE: Früh/Spät/Nacht/Frei). */
export const SHIFT_TYPE_LABEL_KEYS: Readonly<Record<ShiftType, string>> = {
  Morning: 'shifts.type.morning',
  Evening: 'shifts.type.evening',
  Night: 'shifts.type.night',
  Off: 'shifts.type.off',
};

/**
 * Vardiya tipinin kisa gosterimi — izgara hucresi darsa tam etiket sigmaz.
 * Stok ikon kullanilmadigi icin gosterim tipografiktir ve **cevrilebilir**
 * olmalidir (DE "F" ≠ EN "M").
 */
export const SHIFT_TYPE_SHORT_KEYS: Readonly<Record<ShiftType, string>> = {
  Morning: 'shifts.short.morning',
  Evening: 'shifts.short.evening',
  Night: 'shifts.short.night',
  Off: 'shifts.short.off',
};

/** `ShiftResponse` — `date` tarih (`"2026-08-03"`). */
export interface ShiftResponse {
  readonly id: string;
  readonly employeeId: string;
  readonly employeeName: string;
  readonly date: string;
  readonly shiftType: ShiftType;
  readonly note?: string | null;
}

export interface ShiftPlanDay {
  readonly date: string;
  readonly shifts: readonly ShiftResponse[];
}

/** Plan satirlari (calisanlar) — sunucu ad/soyada gore siralar. */
export interface ShiftPlanEmployee {
  readonly id: string;
  readonly fullName: string;
  readonly departmentName?: string | null;
}

/**
 * `GET /shifts` yaniti. `week` yalnizca `?week=` ile sorulduysa doludur;
 * serbest `from`/`to` araliginda `null` doner.
 */
export interface ShiftPlanResponse {
  readonly from: string;
  readonly to: string;
  readonly week?: string | null;
  readonly days: readonly ShiftPlanDay[];
  readonly employees: readonly ShiftPlanEmployee[];
}

/** `POST /shifts` ve `PUT /shifts/{id}` govdesi (alanlar birebir ayni). */
export interface ShiftWriteRequest {
  readonly employeeId: string;
  readonly date: string;
  readonly shiftType: ShiftType;
  readonly note?: string | null;
}

export const SHIFT_LIMITS = {
  noteMaxLength: 500,
} as const;
