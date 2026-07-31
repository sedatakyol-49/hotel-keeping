import { HttpErrorResponse } from '@angular/common/http';

/**
 * ===========================================================================
 * PUBLIC HATA MODELI — "kod gosterme, ne yapacagini soyle"
 * ===========================================================================
 *
 * Sozlesme (§1, §8) her public hatada `extensions.code` icinde **dilden
 * bagimsiz, stabil** bir anahtar dondurur. Istemci mantigi `status` + `code`
 * uzerine kurulur, **mesaj metnine asla** (metin dile gore degisir).
 *
 * Bu dosya iki isi yapar:
 *  1) `HttpErrorResponse` -> `PublicApiError` (kod + alan hatalari + Retry-After),
 *  2) kod -> **kullaniciya gosterilecek** i18n anahtarlari.
 *
 * ONEMLI: kullaniciya `HOLD_EXPIRED` yazmayiz. Her koda bir BASLIK, bir
 * ACIKLAMA ve bir **KURTARMA EYLEMI** anahtari eslesir; ekranlar bu ucluyu
 * gosterir. Kodu yalnizca `data-error-code` niteliginde birakiriz — destek
 * ekibi ve testler icin gorunur, misafir icin degil.
 */

export const PUBLIC_ERROR_CODES = [
  'VALIDATION_FAILED',
  'CARD_DATA_NOT_ACCEPTED',
  'CHANNEL_NOT_CONFIGURED',
  'BRAND_NOT_FOUND',
  'HOTEL_NOT_FOUND',
  'ROOM_TYPE_NOT_FOUND',
  'HOLD_NOT_FOUND',
  'BOOKING_NOT_FOUND',
  'HOLD_EXPIRED',
  'HOLD_ALREADY_USED',
  'ROOM_NO_LONGER_AVAILABLE',
  'CAPACITY_EXCEEDED',
  'SUMMARY_CHANGED',
  'LEGAL_TEXT_CHANGED',
  'CANCELLATION_NOT_ALLOWED',
  'FEE_ACKNOWLEDGEMENT_REQUIRED',
  'BOOKING_ALREADY_CANCELLED',
  'RATE_LIMIT_EXCEEDED',
  'PAYMENT_PROVIDER_UNAVAILABLE',
] as const;

export type PublicErrorCode = (typeof PUBLIC_ERROR_CODES)[number];

/**
 * Kurtarma eylemi — ekran bu degeri okuyup dogru dugmeyi gosterir.
 * Ham hata kodunu degil, **ne yapilacagini** tasir.
 */
export type PublicErrorRecovery =
  | 'retry' // ayni istegi tekrar dene
  | 'renewHold' // yeni teklif al (hold yenile)
  | 'reconfirmSummary' // degisen ozeti yeniden onayla
  | 'backToSearch' // arama sonuclarina don
  | 'changeDates' // tarih/kisi sayisi degistir
  | 'fixForm' // formdaki alanlari duzelt
  | 'contactHotel' // oteli ara
  | 'wait' // hiz siniri: bir sure bekle
  | 'none';

export interface PublicApiError {
  readonly status: number;
  readonly code: PublicErrorCode | null;
  /** Baslik i18n anahtari — her zaman doludur. */
  readonly titleKey: string;
  /** Aciklama i18n anahtari — "ne oldu + ne yapmali". */
  readonly bodyKey: string;
  readonly recovery: PublicErrorRecovery;
  /** Alan bazli dogrulama hatalari (PascalCase anahtarlar). */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>> | null;
  /** 429 icin saniye. */
  readonly retryAfterSeconds: number | null;
  /** Sunucunun teknik aciklamasi — LOG icin, ekranda gosterilmez. */
  readonly detail: string | null;
  readonly traceId: string | null;
}

interface CodePresentation {
  readonly bodyKey: string;
  readonly recovery: PublicErrorRecovery;
  readonly titleKey?: string;
}

/**
 * Kod -> ekran sunumu. Anahtarlar `errors.public.*` altinda yasar; tek bir
 * yerde durmasi, "bir kod eklendi ama metni yok" durumunu gorunur kilar
 * (birim test bu tablonun tamligini dogrular).
 */
const PRESENTATION: Readonly<Record<PublicErrorCode, CodePresentation>> = {
  VALIDATION_FAILED: { bodyKey: 'errors.public.validationFailed', recovery: 'fixForm' },
  CARD_DATA_NOT_ACCEPTED: {
    bodyKey: 'errors.public.cardDataNotAccepted',
    recovery: 'contactHotel',
  },
  CHANNEL_NOT_CONFIGURED: {
    bodyKey: 'errors.public.channelNotConfigured',
    recovery: 'contactHotel',
  },
  BRAND_NOT_FOUND: { bodyKey: 'errors.public.hotelNotFound', recovery: 'none' },
  HOTEL_NOT_FOUND: { bodyKey: 'errors.public.hotelNotFound', recovery: 'none' },
  ROOM_TYPE_NOT_FOUND: { bodyKey: 'errors.public.roomTypeNotFound', recovery: 'backToSearch' },
  HOLD_NOT_FOUND: { bodyKey: 'errors.public.holdNotFound', recovery: 'renewHold' },
  BOOKING_NOT_FOUND: { bodyKey: 'errors.public.bookingNotFound', recovery: 'none' },
  HOLD_EXPIRED: { bodyKey: 'errors.public.holdExpired', recovery: 'renewHold' },
  HOLD_ALREADY_USED: { bodyKey: 'errors.public.holdAlreadyUsed', recovery: 'none' },
  ROOM_NO_LONGER_AVAILABLE: {
    bodyKey: 'errors.public.roomNoLongerAvailable',
    recovery: 'backToSearch',
  },
  CAPACITY_EXCEEDED: { bodyKey: 'errors.public.capacityExceeded', recovery: 'changeDates' },
  SUMMARY_CHANGED: { bodyKey: 'errors.public.summaryChanged', recovery: 'reconfirmSummary' },
  LEGAL_TEXT_CHANGED: { bodyKey: 'errors.public.legalTextChanged', recovery: 'reconfirmSummary' },
  CANCELLATION_NOT_ALLOWED: {
    bodyKey: 'errors.public.cancellationNotAllowed',
    recovery: 'contactHotel',
  },
  FEE_ACKNOWLEDGEMENT_REQUIRED: {
    bodyKey: 'errors.public.feeAcknowledgementRequired',
    recovery: 'reconfirmSummary',
  },
  BOOKING_ALREADY_CANCELLED: {
    bodyKey: 'errors.public.bookingAlreadyCancelled',
    recovery: 'none',
  },
  RATE_LIMIT_EXCEEDED: { bodyKey: 'errors.public.rateLimitExceeded', recovery: 'wait' },
  PAYMENT_PROVIDER_UNAVAILABLE: {
    bodyKey: 'errors.public.paymentProviderUnavailable',
    recovery: 'retry',
  },
};

/** `code` gelmediginde (admin uclari bu alani tasimaz) durum koduna duseriz. */
const STATUS_FALLBACK: Readonly<Record<number, CodePresentation>> = {
  0: { bodyKey: 'errors.public.network', recovery: 'retry' },
  400: { bodyKey: 'errors.public.validationFailed', recovery: 'fixForm' },
  404: { bodyKey: 'errors.public.notFound', recovery: 'backToSearch' },
  408: { bodyKey: 'errors.public.network', recovery: 'retry' },
  409: { bodyKey: 'errors.public.conflict', recovery: 'retry' },
  429: { bodyKey: 'errors.public.rateLimitExceeded', recovery: 'wait' },
  500: { bodyKey: 'errors.public.server', recovery: 'retry' },
  502: { bodyKey: 'errors.public.server', recovery: 'retry' },
  503: { bodyKey: 'errors.public.server', recovery: 'retry' },
  504: { bodyKey: 'errors.public.network', recovery: 'retry' },
};

const GENERIC: CodePresentation = { bodyKey: 'errors.public.unknown', recovery: 'retry' };

function isPublicErrorCode(value: unknown): value is PublicErrorCode {
  return typeof value === 'string' && (PUBLIC_ERROR_CODES as readonly string[]).includes(value);
}

/** RFC 7807 gövdesinden `code` uzantisini cikarir (iki olasi yerde arar). */
function readCode(body: unknown): PublicErrorCode | null {
  if (body === null || typeof body !== 'object') {
    return null;
  }
  const record = body as Record<string, unknown>;
  if (isPublicErrorCode(record['code'])) {
    return record['code'];
  }
  const extensions = record['extensions'];
  if (extensions !== null && typeof extensions === 'object') {
    const nested = (extensions as Record<string, unknown>)['code'];
    if (isPublicErrorCode(nested)) {
      return nested;
    }
  }
  return null;
}

function readFieldErrors(body: unknown): Readonly<Record<string, readonly string[]>> | null {
  if (body === null || typeof body !== 'object') {
    return null;
  }
  const errors = (body as Record<string, unknown>)['errors'];
  if (errors === null || typeof errors !== 'object') {
    return null;
  }

  const result: Record<string, readonly string[]> = {};
  for (const [field, messages] of Object.entries(errors as Record<string, unknown>)) {
    if (Array.isArray(messages)) {
      result[field] = messages.map((message) => String(message));
    }
  }
  return Object.keys(result).length > 0 ? result : null;
}

function readString(body: unknown, key: string): string | null {
  if (body === null || typeof body !== 'object') {
    return null;
  }
  const value = (body as Record<string, unknown>)[key];
  return typeof value === 'string' ? value : null;
}

/** `HttpErrorResponse` (veya beklenmeyen bir sey) -> `PublicApiError`. */
export function toPublicError(error: unknown): PublicApiError {
  if (isPublicApiError(error)) {
    return error;
  }

  if (!(error instanceof HttpErrorResponse)) {
    return build(0, null, GENERIC, null, null, null, null);
  }

  const body: unknown = error.error;
  const code = readCode(body);
  const status = error.status || 0;
  const presentation = code !== null ? PRESENTATION[code] : (STATUS_FALLBACK[status] ?? GENERIC);
  const retryAfter = Number(error.headers?.get('Retry-After') ?? '');

  return build(
    status,
    code,
    presentation,
    readFieldErrors(body),
    Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : null,
    readString(body, 'detail'),
    readString(body, 'traceId'),
  );
}

function build(
  status: number,
  code: PublicErrorCode | null,
  presentation: CodePresentation,
  fieldErrors: Readonly<Record<string, readonly string[]>> | null,
  retryAfterSeconds: number | null,
  detail: string | null,
  traceId: string | null,
): PublicApiError {
  return {
    status,
    code,
    titleKey: presentation.titleKey ?? 'errors.public.title',
    bodyKey: presentation.bodyKey,
    recovery: presentation.recovery,
    fieldErrors,
    retryAfterSeconds,
    detail,
    traceId,
  };
}

export function isPublicApiError(value: unknown): value is PublicApiError {
  return (
    value !== null &&
    typeof value === 'object' &&
    'recovery' in value &&
    'bodyKey' in value &&
    'status' in value
  );
}

/** Sunucunun alan hatalarini duz bir listeye cevirir (hata ozeti icin). */
export function fieldErrorList(error: PublicApiError): readonly string[] {
  if (error.fieldErrors === null) {
    return [];
  }
  return Object.values(error.fieldErrors).flat();
}
