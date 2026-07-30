import type { AppLanguage } from '@hotelcore/shared';

/**
 * Misafir (Gast) modulu tipleri
 * (docs/api-contracts-reservations.md → "Guests").
 *
 * Izin semasi bilinclidir: misafir verisi rezervasyon modulunun parcasidir,
 * bu yuzden ayri bir `Guests.*` izin anahtari **yoktur** — okuma
 * `Reservations.View`, yazma `Reservations.Create` ile korunur.
 */

/**
 * `GuestResponse`.
 *
 * `stayCount` **yalnizca detay/yazma yanitlarinda** doludur; liste yanitinda
 * satir basina korele alt sorgu maliyetinden kacinmak icin `null` doner
 * (sozlesme). Ekran bunu bozmadan gosterir: listede sutun hic cizilmez.
 */
export interface GuestResponse {
  readonly id: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly fullName: string;
  readonly email?: string | null;
  readonly phone?: string | null;
  /** `Country` enum **adi** (`DE | AT | CH | TR`) veya null. */
  readonly nationality?: string | null;
  readonly addressLine?: string | null;
  readonly postalCode?: string | null;
  readonly city?: string | null;
  readonly birthDate?: string | null;
  readonly culture?: string | null;
  readonly note?: string | null;
  /**
   * `CheckedOut` durumundaki rezervasyon sayisi (tamamlanmis konaklamalar).
   * Listede **null**, detayda dolu.
   */
  readonly stayCount?: number | null;
}

/** `GET /guests?page=&pageSize=&search=` — `search`: ad/soyad/e-posta contains. */
export interface GuestListQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly search?: string | null;
}

/** `POST /guests` ve `PUT /guests/{id}` govdesi (tam guncelleme). */
export interface GuestWriteRequest {
  readonly firstName: string;
  readonly lastName: string;
  readonly email?: string | null;
  readonly phone?: string | null;
  readonly nationality?: string | null;
  readonly addressLine?: string | null;
  readonly postalCode?: string | null;
  readonly city?: string | null;
  readonly birthDate?: string | null;
  readonly culture?: AppLanguage | null;
  readonly note?: string | null;
}

/**
 * Sozlesmedeki dogrulama sinirlari (400 + `errors`) — istemcide de uygulanir
 * ki gereksiz 400 yaniti alinmasin; son soz backend'dedir.
 */
export const GUEST_LIMITS = {
  firstNameMaxLength: 100,
  lastNameMaxLength: 100,
  emailMaxLength: 256,
  phoneMaxLength: 32,
  addressLineMaxLength: 256,
  postalCodeMaxLength: 16,
  cityMaxLength: 100,
  noteMaxLength: 1000,
} as const;
