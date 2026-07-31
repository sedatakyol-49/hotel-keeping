import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { describe, expect, it } from 'vitest';

import { PUBLIC_ERROR_CODES, fieldErrorList, toPublicError } from './public-error';

function response(status: number, body: unknown, headers?: Record<string, string>) {
  return new HttpErrorResponse({
    status,
    error: body,
    headers: headers ? new HttpHeaders(headers) : undefined,
  });
}

describe('Public hata eslemesi — istemci mantigi koda dayanir, metne degil', () => {
  it('`extensions.code` degerini okur ve kurtarma yolunu belirler', () => {
    const error = toPublicError(
      response(409, { status: 409, extensions: { code: 'HOLD_EXPIRED' } }),
    );

    expect(error.code).toBe('HOLD_EXPIRED');
    expect(error.recovery).toBe('renewHold');
    expect(error.bodyKey).toBe('errors.public.holdExpired');
  });

  it('kodu ust duzeyde tasiyan gövdeyi de kabul eder', () => {
    const error = toPublicError(response(409, { status: 409, code: 'SUMMARY_CHANGED' }));
    expect(error.code).toBe('SUMMARY_CHANGED');
    expect(error.recovery).toBe('reconfirmSummary');
  });

  it('kod yoksa yalnizca duruma duser (admin uclari `code` tasimaz)', () => {
    const error = toPublicError(response(404, { status: 404, title: 'Not Found' }));

    expect(error.code).toBeNull();
    expect(error.bodyKey).toBe('errors.public.notFound');
  });

  it('KATALOGDAKI HER KOD icin bir metin ve bir kurtarma yolu vardir', () => {
    for (const code of PUBLIC_ERROR_CODES) {
      const error = toPublicError(response(400, { extensions: { code } }));

      expect(error.code, code).toBe(code);
      expect(error.bodyKey, code).toMatch(/^errors\.public\./);
      expect(error.titleKey, code).toBe('errors.public.title');
    }
  });

  it('sunucunun teknik `detail` metnini EKRAN METNI yapmaz', () => {
    const error = toPublicError(
      response(409, {
        status: 409,
        detail: 'Hold 1a2b expired at 09:15:00Z',
        extensions: { code: 'HOLD_EXPIRED' },
      }),
    );

    // `detail` yalnizca teshis icin saklanir; gosterilecek metin bir anahtardir.
    expect(error.detail).toContain('Hold 1a2b');
    expect(error.bodyKey.startsWith('errors.public.')).toBe(true);
  });

  it('429 icin `Retry-After` saniyesini tasir', () => {
    const error = toPublicError(
      response(429, { extensions: { code: 'RATE_LIMIT_EXCEEDED' } }, { 'Retry-After': '42' }),
    );

    expect(error.retryAfterSeconds).toBe(42);
    expect(error.recovery).toBe('wait');
  });

  it('alan bazli dogrulama hatalarini duz listeye cevirir', () => {
    const error = toPublicError(
      response(400, {
        status: 400,
        extensions: { code: 'VALIDATION_FAILED' },
        errors: { Email: ['Ungültig'], LastName: ['Pflichtfeld', 'Zu lang'] },
      }),
    );

    expect(error.recovery).toBe('fixForm');
    expect(fieldErrorList(error)).toEqual(['Ungültig', 'Pflichtfeld', 'Zu lang']);
  });

  it('HTTP disi bir hata da kullanilabilir bir sonuc uretir', () => {
    const error = toPublicError(new Error('boom'));

    expect(error.status).toBe(0);
    expect(error.bodyKey).toBe('errors.public.unknown');
  });
});
