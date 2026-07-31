/**
 * Rezervasyon modulu tipleri
 * (docs/api-contracts-reservations.md → "Reservations" + "Folio").
 *
 * Temel sozlesme karari: konaklama araligi **yari aciktir** `[checkIn, checkOut)` —
 * `checkOut` **dahil degildir**, cikis gunu icin ucret alinmaz ve
 * `nights = checkOut - checkIn`. Istemci gece sayisini kendisi hesaplamaz;
 * sunucunun dondurdugu `nights` degerini gosterir.
 */

/** Rezervasyon durumu — backend enum'unun **adi** (sayi degil) tasinir. */
export const RESERVATION_STATUSES = [
  'Option',
  'Confirmed',
  'CheckedIn',
  'CheckedOut',
  'Cancelled',
  'NoShow',
] as const;

export type ReservationStatus = (typeof RESERVATION_STATUSES)[number];

export function isReservationStatus(value: unknown): value is ReservationStatus {
  return typeof value === 'string' && (RESERVATION_STATUSES as readonly string[]).includes(value);
}

/** Durum -> i18n anahtari (`reservations.status.*`). */
export const RESERVATION_STATUS_LABEL_KEYS: Readonly<Record<ReservationStatus, string>> = {
  Option: 'reservations.status.option',
  Confirmed: 'reservations.status.confirmed',
  CheckedIn: 'reservations.status.checkedIn',
  CheckedOut: 'reservations.status.checkedOut',
  Cancelled: 'reservations.status.cancelled',
  NoShow: 'reservations.status.noShow',
};

/** Satis kanali — backend `ReservationChannel` enum adlari. */
export const RESERVATION_CHANNELS = [
  'Direct',
  'Phone',
  'WalkIn',
  'BookingCom',
  'Hrs',
  'Expedia',
  'Corporate',
  /*
   * Misafire acik web kanali. Backend enum'una eklendi ama bu listeye
   * girmemisti; sonucu iki yerde gorunuyordu: website rezervasyonlarinin kanal
   * etiketi BOS kaliyordu ve fiyat plani formunda "Website" secilemiyordu —
   * oysa fiyat secimi kanali birebir karsilastirir, yani bir Website plani
   * olmadan web fiyati sessizce oda tipinin liste fiyatina duser (ayarlar
   * yaniti bunu `NoRatePlanForWebsiteChannel` uyarisiyla bildirir).
   */
  'Website',
] as const;

export type ReservationChannel = (typeof RESERVATION_CHANNELS)[number];

export function isReservationChannel(value: unknown): value is ReservationChannel {
  return typeof value === 'string' && (RESERVATION_CHANNELS as readonly string[]).includes(value);
}

/** Kanal -> i18n anahtari (`reservations.channel.*`). */
export const RESERVATION_CHANNEL_LABEL_KEYS: Readonly<Record<ReservationChannel, string>> = {
  Direct: 'reservations.channel.direct',
  Phone: 'reservations.channel.phone',
  WalkIn: 'reservations.channel.walkIn',
  BookingCom: 'reservations.channel.bookingCom',
  Hrs: 'reservations.channel.hrs',
  Expedia: 'reservations.channel.expedia',
  Corporate: 'reservations.channel.corporate',
  Website: 'reservations.channel.website',
};

/**
 * Durum makinesi — `ReservationStatusMachine.cs` ile **birebir** aynidir.
 * Ekran yalnizca izin verilen gecisin aksiyonunu render eder; gecersiz gecis
 * sunucuda 409 uretirdi ve kullaniciya yasak yolu gostermek dogru degildir.
 */
export const RESERVATION_TRANSITIONS: Readonly<
  Record<ReservationStatus, readonly ReservationStatus[]>
> = {
  Option: ['Confirmed', 'CheckedIn', 'Cancelled', 'NoShow'],
  Confirmed: ['CheckedIn', 'Cancelled', 'NoShow'],
  CheckedIn: ['CheckedOut'],
  CheckedOut: [],
  Cancelled: [],
  NoShow: [],
};

export function canTransition(from: ReservationStatus, to: ReservationStatus): boolean {
  return RESERVATION_TRANSITIONS[from].includes(to);
}

/** `Option`/`Confirmed` -> `CheckedIn`. */
export function canCheckIn(status: ReservationStatus): boolean {
  return canTransition(status, 'CheckedIn');
}

/** Yalnizca `CheckedIn` -> `CheckedOut`. */
export function canCheckOut(status: ReservationStatus): boolean {
  return canTransition(status, 'CheckedOut');
}

/** `CheckedIn`/`CheckedOut` iptal edilemez (sunucu 409 doner). */
export function canCancel(status: ReservationStatus): boolean {
  return canTransition(status, 'Cancelled');
}

export function canMarkNoShow(status: ReservationStatus): boolean {
  return canTransition(status, 'NoShow');
}

/** Nihai durumda icerik degisikligi (`PUT`) de yasaktir. */
export function isFinalStatus(status: ReservationStatus): boolean {
  return RESERVATION_TRANSITIONS[status].length === 0;
}

export function canEditContent(status: ReservationStatus): boolean {
  return !isFinalStatus(status);
}

/**
 * `Cancelled`/`NoShow` oda takvimini bloke etmez ve doluluk izgarasinda
 * **gorunmez** (sozlesme: grid sozlesmesi).
 */
export function blocksInventory(status: ReservationStatus): boolean {
  return status !== 'Cancelled' && status !== 'NoShow';
}

/** `ReservationResponse` — `nights` ve `depositAmount` sunucuda hesaplanir. */
export interface ReservationResponse {
  readonly id: string;
  readonly reservationNumber: string;
  readonly status: ReservationStatus;
  readonly channel: ReservationChannel;
  readonly roomId: string;
  readonly roomNumber: string;
  readonly roomTypeId: string;
  readonly roomTypeCode: string;
  readonly guestId: string;
  readonly guestName: string;
  readonly guestEmail?: string | null;
  readonly checkIn: string;
  readonly checkOut: string;
  readonly nights: number;
  readonly adults: number;
  readonly children: number;
  /** **Sunucu hesaplar** — istemci hicbir zaman gondermez. */
  readonly totalAmount: number;
  readonly currency: string;
  readonly depositPercent: number;
  readonly depositAmount: number;
  readonly ratePlanId?: string | null;
  readonly ratePlanName?: string | null;
  readonly notes?: string | null;
  readonly checkedInAt?: string | null;
  readonly checkedOutAt?: string | null;
  readonly folioId?: string | null;
  /**
   * Misafir kanalindan gelen rezervasyonun **misafire gosterilen** referansi
   * (`K7QM-3XPD-9RTV`); resepsiyondan girilen rezervasyonlarda `null`.
   *
   * NEDEN GOSTERILIYOR: telefonda ve e-postada misafir bu numarayi soyler;
   * `reservationNumber` (RES-2026-00042) ic/ticari referanstir ve misafire hic
   * verilmez (sozlesme §7.1). Alan yanitta vardi ama hicbir ekranda
   * gorunmuyordu — resepsiyon, misafirin okudugu numarayla kaydi bulamazdi.
   */
  readonly publicReference?: string | null;
}

/**
 * `GET /reservations` filtreleri:
 * `?page&pageSize&status=&channel=&roomId=&guestId=&from=&to=&search=`
 *
 * `from`/`to` **aralikla kesisen** konaklamalari suzer
 * (`from < checkOut && checkIn < to`), yani tarihleri kapsayan degil kesisen.
 */
export interface ReservationListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly status?: ReservationStatus | null;
  readonly channel?: ReservationChannel | null;
  readonly roomId?: string | null;
  readonly guestId?: string | null;
  readonly from?: string | null;
  readonly to?: string | null;
  readonly search?: string | null;
}

/**
 * `POST /reservations` govdesi.
 *
 * **`totalAmount` alani bilincli olarak YOKTUR**: fiyat manipulasyonunu
 * onlemek icin tutar her zaman sunucuda hesaplanir (sozlesme §"totalAmount —
 * her zaman sunucuda"). Gonderilse de yok sayilirdi; tip seviyesinde de
 * gonderilemez olmasi istemci hatasini derleme zamanina tasir.
 */
export interface CreateReservationRequest {
  readonly roomId: string;
  readonly guestId: string;
  readonly checkIn: string;
  readonly checkOut: string;
  readonly adults: number;
  readonly children: number;
  readonly channel: ReservationChannel;
  readonly depositPercent: number;
  readonly notes?: string | null;
  /** Yalnizca `Option` veya `Confirmed`; verilmezse sunucu `Option` kullanir. */
  readonly status?: Extract<ReservationStatus, 'Option' | 'Confirmed'> | null;
}

/** `PUT /reservations/{id}` — `status` **tasinmaz** (durum yalnizca aksiyonlarla degisir). */
export type UpdateReservationRequest = Omit<CreateReservationRequest, 'status'>;

/** `POST /reservations/{id}/cancel` — govde ve alan opsiyoneldir. */
export interface CancelReservationRequest {
  readonly reason?: string | null;
}

/** Folio satir tipi (`RoomCharge | Extra | CityTax`). */
export const FOLIO_LINE_TYPES = ['RoomCharge', 'Extra', 'CityTax'] as const;

export type FolioLineType = (typeof FOLIO_LINE_TYPES)[number];

/** Satir tipi -> i18n anahtari (fatura satirlariyla **ayni** anahtar grubu). */
export const FOLIO_LINE_TYPE_LABEL_KEYS: Readonly<Record<FolioLineType, string>> = {
  RoomCharge: 'invoices.lineType.roomCharge',
  Extra: 'invoices.lineType.extra',
  CityTax: 'invoices.lineType.cityTax',
};

/**
 * Folio satiri. Fiyatlar **brut**tur; `lineNet + lineVat = lineGross` her zaman
 * tutar. `unitPrice` gosterim icindir (gece ortalamasi).
 */
export interface FolioLineResponse {
  readonly id: string;
  readonly type: FolioLineType;
  readonly description: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly vatRate: number;
  readonly lineNet: number;
  readonly lineVat: number;
  readonly lineGross: number;
  /** Leistungsdatum (GoBD). */
  readonly serviceDate: string;
}

/**
 * `GET /reservations/{id}/folio`.
 * Folio henuz acilmamissa `folioId: null`, `lines: []` ve toplamlar `0` doner —
 * istemci ayri bir "folio yok" durumu ele almaz.
 */
export interface FolioResponse {
  readonly reservationId: string;
  readonly reservationNumber: string;
  readonly folioId?: string | null;
  readonly isClosed: boolean;
  readonly currency: string;
  readonly guestName: string;
  readonly lines: readonly FolioLineResponse[];
  readonly totalNet: number;
  readonly totalVat: number;
  readonly totalGross: number;
}

/** Sozlesmedeki dogrulama sinirlari (400 + `errors`). */
export const RESERVATION_LIMITS = {
  adultsMin: 1,
  adultsMax: 20,
  childrenMin: 0,
  childrenMax: 20,
  /** Konaklama en fazla 365 gece. */
  maxNights: 365,
  depositPercentMin: 0,
  depositPercentMax: 100,
  notesMaxLength: 1000,
  cancelReasonMaxLength: 500,
} as const;
