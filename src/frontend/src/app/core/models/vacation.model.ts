/**
 * Izin (Urlaub) modulu tipleri (api-contracts.md → "Izin (Urlaub)").
 *
 * Onemli sozlesme notu: `requestedDays` **takvim gunudur** — hafta sonu ve
 * resmi tatil mantigi bu fazda yoktur. Ekran bunu kullaniciya aciklama satiri
 * olarak belirtir; istemci gun sayisini kendisi hesaplamaz, sunucunun
 * dondurdugu degeri gosterir.
 */

/** Talep durumu — backend enum'unun **adi** (sayi degil) tasinir. */
export const VACATION_STATUSES = ['Pending', 'Approved', 'Rejected', 'Cancelled'] as const;

export type VacationStatus = (typeof VACATION_STATUSES)[number];

export function isVacationStatus(value: unknown): value is VacationStatus {
  return typeof value === 'string' && (VACATION_STATUSES as readonly string[]).includes(value);
}

/** Durum -> i18n anahtari (`vacations.status.*`). */
export const VACATION_STATUS_LABEL_KEYS: Readonly<Record<VacationStatus, string>> = {
  Pending: 'vacations.status.pending',
  Approved: 'vacations.status.approved',
  Rejected: 'vacations.status.rejected',
  Cancelled: 'vacations.status.cancelled',
};

/**
 * Karara baglanmis talepte karar aksiyonlari (`approve`/`reject`) gizlenir:
 * sozlesme geregi yalnizca `Pending` talep onaylanip reddedilebilir, aksi
 * halde 409 doner.
 */
export function isDecidable(status: VacationStatus): boolean {
  return status === 'Pending';
}

/** `Cancelled`/`Rejected` bir talep artik iptal edilemez. */
export function isCancellable(status: VacationStatus): boolean {
  return status === 'Pending' || status === 'Approved';
}

/**
 * `VacationRequestResponse` — api-contracts.md / Izin.
 * `from` / `to` **tarih** (saat yok): `"2026-08-10"`.
 * `createdAt` sozlesme metninde yazili degildir ama OpenAPI semasinda ve
 * canli yanitta vardir (talep tarihi); listede bilgi olarak gosterilir.
 */
export interface VacationRequestResponse {
  readonly id: string;
  readonly employeeId: string;
  readonly employeeName: string;
  readonly from: string;
  readonly to: string;
  /** Takvim gunu (hafta sonu/tatil dusulmez). */
  readonly requestedDays: number;
  readonly status: VacationStatus;
  readonly reason?: string | null;
  readonly decidedByUserId?: string | null;
  readonly decidedAt?: string | null;
  readonly decisionNote?: string | null;
  readonly createdAt?: string | null;
}

/**
 * `VacationBalanceResponse` — kalici satir yoksa calisanin `annualLeaveDays`
 * degerinden turetilir ve **`id: null`** doner. Ekran bunu bozulmadan gosterir
 * (bakiye satiri "henuz olusmadi" olarak isaretlenir, sayilar yine gecerlidir).
 */
export interface VacationBalanceResponse {
  readonly id?: string | null;
  readonly employeeId: string;
  readonly employeeName: string;
  readonly year: number;
  readonly entitledDays: number;
  readonly usedDays: number;
  readonly carriedOverDays: number;
  readonly remainingDays: number;
}

/**
 * `GET /vacations` filtreleri:
 * `?page&pageSize&employeeId=&status=&year=&from=&to=`
 */
export interface VacationListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly employeeId?: string | null;
  readonly status?: VacationStatus | null;
  readonly year?: number | null;
  readonly from?: string | null;
  readonly to?: string | null;
}

/** `GET /vacations/balances` filtreleri (ikisi de opsiyonel). */
export interface VacationBalanceQuery {
  readonly employeeId?: string | null;
  readonly year?: number | null;
}

/** `POST /vacations` govdesi. */
export interface CreateVacationRequest {
  readonly employeeId: string;
  readonly from: string;
  readonly to: string;
  readonly reason?: string | null;
}

/**
 * `approve` / `reject` / `cancel` govdesi — tek alan (`decisionNote`) ve
 * govdenin kendisi opsiyoneldir.
 */
export interface VacationDecisionRequest {
  readonly decisionNote?: string | null;
}

/** Sozlesmede yazili olmayan uzunluk sinirlari icin makul istemci tavani. */
export const VACATION_LIMITS = {
  reasonMaxLength: 500,
  decisionNoteMaxLength: 500,
} as const;
