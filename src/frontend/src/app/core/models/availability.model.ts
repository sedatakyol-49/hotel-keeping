import type { ReservationStatus } from './reservation.model';

/**
 * Musaitlik + doluluk tipleri
 * (docs/api-contracts-reservations.md → "Availability & Occupancy").
 *
 * Her iki uc da **aktif otel gerektirir**: matris/liste tek bir otele aittir.
 * Head Office kullanicisi `X-Hotel-Id` gondermezse sunucu 400 doner
 * (`errors: { "X-Hotel-Id": [...] }`).
 */

/** `GET /availability?from=&to=&roomTypeId=` */
export interface AvailabilityQuery {
  readonly from: string;
  /** **Dahil degil** (yari acik aralik); `to > from` zorunlu. */
  readonly to: string;
  readonly roomTypeId?: string | null;
}

/** Musait oda satiri — **fiyat alani yoktur** (tutar yalnizca sunucuda hesaplanir). */
export interface AvailableRoomResponse {
  readonly roomId: string;
  readonly roomNumber: string;
  readonly floor: number;
  readonly roomTypeId: string;
  readonly roomTypeCode: string;
  readonly capacity: number;
}

export interface AvailabilityByRoomType {
  readonly roomTypeId: string;
  readonly roomTypeCode: string;
  readonly availableRoomCount: number;
}

export interface AvailabilityResponse {
  readonly from: string;
  readonly to: string;
  readonly nights: number;
  readonly roomTypeId?: string | null;
  /** Filtreye uyan tum odalar (servis disi dahil). */
  readonly totalRoomCount: number;
  /** Musait sayilmazlar. */
  readonly outOfOrderRoomCount: number;
  /** Aralik boyunca **tum** geceleri bos olan odalar. */
  readonly availableRoomCount: number;
  readonly byRoomType: readonly AvailabilityByRoomType[];
  readonly rooms: readonly AvailableRoomResponse[];
}

/** `GET /occupancy?from=&to=` */
export interface OccupancyQuery {
  readonly from: string;
  /** **Dahil degil**; `days` dizisi `[from, to)` araligini verir. */
  readonly to: string;
}

/**
 * Bir hucre = **bir oda + bir gece**.
 *
 * `isArrival` ilk gece (`date == checkIn`), `isDeparture` **son gece**
 * (`date == checkOut - 1`) demektir: misafir ertesi sabah cikar ve cikis gunu
 * icin hucre uretilmez. Boylece izgara cubugu dogru yerde biter ve ardisik
 * konaklamalar yan yana durabilir.
 */
export interface OccupancyCellResponse {
  readonly date: string;
  readonly reservationId: string;
  readonly reservationNumber: string;
  readonly guestName: string;
  readonly status: ReservationStatus;
  readonly isArrival: boolean;
  readonly isDeparture: boolean;
}

/** Oda satiri; `cells` **seyrektir** — yalnizca dolu geceler icin hucre vardir. */
export interface OccupancyRoomResponse {
  readonly roomId: string;
  readonly roomNumber: string;
  readonly floor: number;
  readonly roomTypeId: string;
  readonly roomTypeCode: string;
  readonly isOutOfOrder: boolean;
  readonly cells: readonly OccupancyCellResponse[];
}

export interface OccupancySummaryResponse {
  readonly roomCount: number;
  readonly days: number;
  readonly roomNights: number;
  readonly occupiedRoomNights: number;
  /** Yuzde (ornek: `5.56`). */
  readonly occupancyRate: number;
}

export interface OccupancyResponse {
  readonly from: string;
  readonly to: string;
  /** Kolon ekseni; `from` dahil, `to` haric. */
  readonly days: readonly string[];
  readonly rooms: readonly OccupancyRoomResponse[];
  readonly summary: OccupancySummaryResponse;
}

/**
 * Sunucu araligi sinirlar (400):
 * `/occupancy` en fazla **92 gun** (yanit oda × gun carpimsal buyur),
 * `/availability` en fazla **366 gun** (oda basina tek satir).
 *
 * Istemci bu sinirlari **istek gondermeden once** uygular: kullanici gecersiz
 * bir adres yazsa bile ekran kirilmaz ve bos yere 400 alinmaz.
 */
export const OCCUPANCY_MAX_DAYS = 92;
export const AVAILABILITY_MAX_DAYS = 366;

/** Iki ISO tarih arasindaki **gece** sayisi (`to - from`); ters/gecersizde `null`. */
export function nightsBetween(from: string, to: string): number | null {
  const start = Date.parse(`${from}T00:00:00Z`);
  const end = Date.parse(`${to}T00:00:00Z`);
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return null;
  }
  return Math.round((end - start) / 86_400_000);
}

/** ISO tarihe gun ekler (UTC hesabi — yaz saati kaydirmaz). */
export function addDays(date: string, days: number): string {
  const parsed = Date.parse(`${date}T00:00:00Z`);
  if (Number.isNaN(parsed)) {
    return date;
  }
  return new Date(parsed + days * 86_400_000).toISOString().slice(0, 10);
}

/** Bugunun ISO tarihi (UTC). */
export function todayIso(now: Date = new Date()): string {
  return new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()),
  )
    .toISOString()
    .slice(0, 10);
}
