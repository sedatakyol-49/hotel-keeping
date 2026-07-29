/**
 * Ulke kodu — backend `Country` enum'unun string karsiligi (`DE`, `AT`, `CH`, `TR`, ...).
 * Yeni ulkeler backend tarafinda eklendiginde frontend'in degismesi gerekmesin diye
 * bilincli olarak acik uclu (string) birakildi.
 */
export type CountryCode = string;

/**
 * Otel ozet bilgisi. `/auth/me` ve `/hotels` yanitlarinda kullanilir;
 * hotel switcher yalnizca bu alanlara ihtiyac duyar.
 */
export interface HotelSummary {
  readonly id: string;
  readonly name: string;
  readonly city?: string | null;
  readonly country?: CountryCode | null;
  readonly currency?: string | null;
  readonly defaultCulture?: string | null;
}
