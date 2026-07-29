/**
 * RFC 7807 ProblemDetails — backend tum hatalari bu formatta dondurur
 * (api-contracts.md — Genel Kurallar).
 */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  /** ValidationProblemDetails: alan adi -> hata mesajlari. */
  readonly errors?: Readonly<Record<string, readonly string[]>>;
  readonly traceId?: string;
}

/** Uygulama genelinde tasinan normalize edilmis hata nesnesi. */
export interface ApiError {
  /** HTTP durum kodu; ag hatasinda 0. */
  readonly status: number;
  /** Kullaniciya gosterilecek i18n anahtari (ornek: `errors.forbidden`). */
  readonly messageKey: string;
  /** Backend'den gelen ham `detail`/`title` — teknik log/detay icin. */
  readonly detail?: string;
  /** Alan bazli dogrulama hatalari. */
  readonly fieldErrors?: Readonly<Record<string, readonly string[]>>;
  readonly traceId?: string;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate['title'] === 'string' ||
    typeof candidate['detail'] === 'string' ||
    typeof candidate['type'] === 'string' ||
    typeof candidate['status'] === 'number'
  );
}
