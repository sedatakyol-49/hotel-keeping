import type { AppLanguage } from './language.model';

/**
 * Oda tipi cevirisi (`Translation` tablosu, mimari §4.6).
 * Yazma uclarinda tum diller opsiyoneldir; okuma tarafinda yalnizca
 * `GET /room-types/{id}` yaniti tum cevirileri birlikte dondurur.
 */
export interface RoomTypeTranslation {
  readonly name?: string | null;
  readonly description?: string | null;
}

/** `{ "de": { name, description }, "en": { ... } }` seklindeki ceviri sozlugu. */
export type RoomTypeTranslations = Partial<Record<AppLanguage, RoomTypeTranslation>>;

/**
 * `RoomTypeResponse` — api-contracts.md → Rooms & Housekeeping / Sekiller.
 * `name`/`description` `Accept-Language`'e gore cozumlenmis metinlerdir.
 */
export interface RoomTypeResponse {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly description?: string | null;
  readonly basePrice: number;
  readonly currency: string;
  readonly capacity: number;
  readonly sizeSqm?: number | null;
  /** DB'de virgullu string, API'de dizi. */
  readonly amenities: readonly string[];
  /** Bagli oda sayisi — silme onayinda uyari icin kullanilir. */
  readonly roomCount: number;
  /** Yalnizca tek kayit yanitinda gelir (duzenleme ekrani icin). */
  readonly translations?: RoomTypeTranslations;
}

/**
 * `POST /room-types` ve `PUT /room-types/{id}` govdesi.
 *
 * `name`/`description` entity'nin varsayilan (fallback) degerleridir; dile ozel
 * metinler `translations` altinda gonderilir.
 */
export interface RoomTypeWriteRequest {
  readonly code: string;
  readonly name: string;
  readonly description?: string | null;
  readonly basePrice: number;
  readonly capacity: number;
  readonly sizeSqm?: number | null;
  readonly amenities: readonly string[];
  readonly translations?: RoomTypeTranslations;
}

export type CreateRoomTypeRequest = RoomTypeWriteRequest;
export type UpdateRoomTypeRequest = RoomTypeWriteRequest;

/**
 * Sozlesmedeki dogrulama kurallari (400 + `errors`) — istemci tarafinda da
 * birebir uygulanir, ancak son soz backend'dedir.
 *
 * `nameMaxLength`, `descriptionMaxLength` ve donanim sinirlari sozlesme metninde
 * yazili degildir; backend `RoomTypeWriteValidator`/`AmenityList` ile ayni degerler
 * kullanilarak gereksiz 400 yaniti onlenir.
 */
export const ROOM_TYPE_LIMITS = {
  codeMinLength: 1,
  codeMaxLength: 10,
  nameMaxLength: 150,
  descriptionMaxLength: 1000,
  basePriceMin: 0,
  capacityMin: 1,
  capacityMax: 20,
  /** `sizeSqm` tam sayidir ve `> 0` olmalidir (veya `null`). */
  sizeSqmMin: 1,
  amenityMaxLength: 50,
  amenityMaxCount: 30,
} as const;
