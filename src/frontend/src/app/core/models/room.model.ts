/**
 * Housekeeping durumu — backend enum'unun **adi** (sayi degil) tasinir
 * (api-contracts.md → Rooms & Housekeeping).
 */
export const HOUSEKEEPING_STATUSES = ['Clean', 'Dirty', 'Inspected', 'OutOfOrder'] as const;

export type HousekeepingStatus = (typeof HOUSEKEEPING_STATUSES)[number];

export function isHousekeepingStatus(value: unknown): value is HousekeepingStatus {
  return typeof value === 'string' && (HOUSEKEEPING_STATUSES as readonly string[]).includes(value);
}

/** Durum -> i18n anahtari (`rooms.housekeeping.status.*`). */
export const HOUSEKEEPING_STATUS_LABEL_KEYS: Readonly<Record<HousekeepingStatus, string>> = {
  Clean: 'rooms.housekeeping.status.clean',
  Dirty: 'rooms.housekeeping.status.dirty',
  Inspected: 'rooms.housekeeping.status.inspected',
  OutOfOrder: 'rooms.housekeeping.status.outOfOrder',
};

/** `RoomResponse` — api-contracts.md / Sekiller. */
export interface RoomResponse {
  readonly id: string;
  readonly number: string;
  readonly floor: number;
  readonly roomTypeId: string;
  readonly roomTypeCode: string;
  readonly roomTypeName: string;
  readonly housekeepingStatus: HousekeepingStatus;
  readonly isOutOfOrder: boolean;
  readonly note?: string | null;
}

/**
 * `GET /rooms` filtreleri: `?page=1&pageSize=20&roomTypeId=&floor=&housekeepingStatus=&search=`
 * (`search` → oda numarasinda contains, buyuk/kucuk harf duyarsiz).
 */
export interface RoomListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly roomTypeId?: string | null;
  readonly floor?: number | null;
  readonly housekeepingStatus?: HousekeepingStatus | null;
  readonly search?: string | null;
}

/** `POST /rooms` ve `PUT /rooms/{id}` govdesi. */
export interface RoomWriteRequest {
  readonly number: string;
  readonly floor: number;
  readonly roomTypeId: string;
  readonly housekeepingStatus: HousekeepingStatus;
  /**
   * Sozlesme geregi `housekeepingStatus === 'OutOfOrder'` ile **tutarli** tutulur;
   * bu yuzden istemci bunu durumdan turetir, ayri bir alan olarak sormaz.
   */
  readonly isOutOfOrder: boolean;
  readonly note?: string | null;
}

export type CreateRoomRequest = RoomWriteRequest;
export type UpdateRoomRequest = RoomWriteRequest;

/** `PATCH /rooms/{id}/housekeeping` — `note` null gonderilirse temizlenir. */
export interface UpdateHousekeepingRequest {
  readonly status: HousekeepingStatus;
  readonly note?: string | null;
}

/** `GET /rooms/board` — finansal alan **icermez** (mimari §7). */
export interface HousekeepingBoardRoom {
  readonly id: string;
  readonly number: string;
  readonly roomTypeCode: string;
  readonly housekeepingStatus: HousekeepingStatus;
  readonly isOutOfOrder: boolean;
  readonly note?: string | null;
}

export interface HousekeepingBoardFloor {
  readonly floor: number;
  readonly rooms: readonly HousekeepingBoardRoom[];
}

export interface HousekeepingSummary {
  readonly clean: number;
  readonly dirty: number;
  readonly inspected: number;
  readonly outOfOrder: number;
  readonly total: number;
}

export interface HousekeepingBoardResponse {
  readonly floors: readonly HousekeepingBoardFloor[];
  readonly summary: HousekeepingSummary;
}

/** Durum -> `summary` alan adi eslesmesi (sayac gosterimi icin). */
export const HOUSEKEEPING_SUMMARY_FIELDS: Readonly<
  Record<HousekeepingStatus, keyof Omit<HousekeepingSummary, 'total'>>
> = {
  Clean: 'clean',
  Dirty: 'dirty',
  Inspected: 'inspected',
  OutOfOrder: 'outOfOrder',
};

/**
 * Sozlesmedeki dogrulama kurallari (400 + `errors`).
 * `noteMaxLength` sozlesme metninde yazili degildir; backend `RoomWriteValidator`
 * ile ayni deger kullanilarak gereksiz 400 yaniti onlenir.
 */
export const ROOM_LIMITS = {
  numberMinLength: 1,
  numberMaxLength: 10,
  floorMin: -5,
  floorMax: 99,
  noteMaxLength: 500,
} as const;
