import type { AppLanguage } from './language.model';

/**
 * Otel vergi profili — docs/api-contracts.md → "Hotels & Ayarlar".
 * Oranlar yuzde olarak tutulur (19 = %19).
 */
export interface TaxProfile {
  readonly vatRate: number;
  readonly reducedVatRate: number;
  /** Kurtaxe: kisi basi / gece tutar (para birimi otelin `currency` alani). */
  readonly cityTaxPerPersonNight: number;
  readonly cityTaxEnabled: boolean;
}

/** `GET /hotels` satiri — otel secici ve ayarlar listesi icin. */
export interface HotelListItemResponse {
  readonly id: string;
  readonly name: string;
  readonly city: string;
  /** Ulke enum **adi** (`DE | AT | CH | TR`), sayi degil. */
  readonly country: string;
  readonly currency: string;
  readonly defaultCulture: string;
  readonly roomCount: number;
}

/** `GET /hotels/{id}` — kunye + vergi profili. */
export interface HotelResponse extends HotelListItemResponse {
  readonly headOfficeId: string;
  readonly addressLine: string | null;
  readonly postalCode: string | null;
  readonly phone: string | null;
  readonly email: string | null;
  readonly taxNumber: string | null;
  readonly taxProfile: TaxProfile;
}

/** `PUT /hotels/{id}/settings` govdesi. */
export interface UpdateHotelSettingsRequest {
  readonly name: string;
  readonly country: string;
  readonly city: string;
  readonly addressLine: string | null;
  readonly postalCode: string | null;
  readonly phone: string | null;
  readonly email: string | null;
  readonly taxNumber: string | null;
  readonly defaultCulture: AppLanguage;
  readonly currency: string;
  readonly taxProfile: TaxProfile;
}

/** `GET|PUT /head-office/settings`. */
export interface HeadOfficeSettingsResponse {
  readonly id: string;
  readonly brandName: string;
  readonly defaultCulture: string;
  readonly hotelCount: number;
}

/** `PUT /head-office/settings` govdesi. */
export interface UpdateHeadOfficeSettingsRequest {
  readonly brandName: string;
  readonly defaultCulture: AppLanguage;
}

/**
 * Backend `Country` enum'unun adlari. Sunucu enum **adini** (string) dondurur/bekler.
 * Yeni ulke eklenince burasi da guncellenmelidir — bilincli olarak dar tutuldu ki
 * form bir metin kutusu degil secim listesi olabilsin.
 */
export const COUNTRIES: readonly string[] = ['DE', 'AT', 'CH', 'TR'];

/** Backend validator'lariyla ayni sinirlar (`UpdateHotelSettingsValidator`). */
export const SETTINGS_LIMITS = {
  nameMaxLength: 200,
  cityMaxLength: 100,
  addressMaxLength: 200,
  postalCodeMaxLength: 20,
  phoneMaxLength: 50,
  emailMaxLength: 200,
  taxNumberMaxLength: 50,
  brandNameMaxLength: 200,
  ratePercentMax: 100,
} as const;
