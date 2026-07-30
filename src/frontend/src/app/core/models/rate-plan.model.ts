import type { ReservationChannel } from './reservation.model';

/**
 * Fiyat plani tipleri (docs/api-contracts-reservations.md → "Rate Plans").
 *
 * **Onemli aralik farki:** `validFrom`/`validTo` **KAPALI** araliktir
 * (`validTo` dahil), cunku plan bir *gun kumesi* tanimlar; konaklama ise
 * *gece kumesi* olarak yari acik araliktir. Tek gunluk plan gecerlidir.
 */
export interface RatePlanResponse {
  readonly id: string;
  readonly roomTypeId: string;
  readonly roomTypeCode: string;
  readonly roomTypeName: string;
  readonly name: string;
  readonly price: number;
  readonly currency: string;
  readonly validFrom: string;
  /** **Dahil** (kapali aralik). */
  readonly validTo: string;
  /** `null` = **tum kanallar**. */
  readonly channel?: ReservationChannel | null;
  readonly isActive: boolean;
}

/** `GET /rate-plans?roomTypeId=&date=` — duz dizi doner (sayfalama yok). */
export interface RatePlanListQuery {
  readonly roomTypeId?: string | null;
  /** O gun gecerli planlar (`validFrom <= date <= validTo`). */
  readonly date?: string | null;
}

/** `POST /rate-plans` ve `PUT /rate-plans/{id}` govdesi. */
export interface RatePlanWriteRequest {
  readonly roomTypeId: string;
  readonly name: string;
  readonly price: number;
  readonly validFrom: string;
  readonly validTo: string;
  /** Verilmezse/`null` ise tum kanallar icin gecerlidir. */
  readonly channel?: ReservationChannel | null;
  readonly isActive: boolean;
}

/** Sozlesmedeki dogrulama sinirlari (400 + `errors`). */
export const RATE_PLAN_LIMITS = {
  nameMaxLength: 150,
  priceMin: 0,
  priceMax: 100_000,
} as const;
