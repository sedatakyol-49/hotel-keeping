import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { API, CANCELLATION, booking, problem } from '../../../testing/public-fixtures';
import {
  configureGuestTestBed,
  renderRoute,
  useTestTranslations,
} from '../../../testing/guest-test-bed';
import type { PublicBookingResponse } from '../../core/api/public-models';

const TOKEN = 'hQ7pR2vK9mNc4XsA1TjW6bYdZ0f';

function http(): HttpTestingController {
  return TestBed.inject(HttpTestingController);
}

async function openBooking(
  response: PublicBookingResponse = booking(),
): Promise<HTMLElement> {
  const element = await renderRoute(`/de/manage/${TOKEN}`);
  http().expectOne(API.booking(TOKEN)).flush(response);
  TestBed.tick();
  return element;
}

function text(element: HTMLElement, selector: string): string {
  return (element.querySelector(selector)?.textContent ?? '').replace(/\s+/gu, ' ').trim();
}

function click(element: HTMLElement, testId: string): void {
  element.querySelector<HTMLButtonElement>(`[data-testid="${testId}"]`)?.click();
  TestBed.tick();
}

beforeEach(() => {
  configureGuestTestBed();
  /* Ucretin GERCEKTEN gosterildigini dogrulamak icin iskelet sablonlar. */
  useTestTranslations({
    manage: {
      cancel: { feeDue: 'Gebuehr {{amount}}', acknowledge: 'Ich weiss: {{amount}}' },
    },
  });
});

describe('Sorgulama — numaralandirma korumasi (§7.4)', () => {
  it('gonderimden sonra rezervasyonun VAR OLDUGUNU dogrulamayan bir metin gosterir', async () => {
    const element = await renderRoute('/de/manage');
    TestBed.tick();

    const reference = element.querySelector<HTMLInputElement>(
      '[data-testid="field-bookingReference"]',
    );
    const email = element.querySelector<HTMLInputElement>('[data-testid="field-email"]');
    reference!.value = 'K7QM-3XPD-9RTV';
    reference!.dispatchEvent(new Event('input'));
    email!.value = 'juergen.mueller@example.de';
    email!.dispatchEvent(new Event('input'));
    TestBed.tick();

    element.querySelector<HTMLButtonElement>('[data-testid="lookup-submit"]')?.click();
    TestBed.tick();

    const request = http().expectOne(API.lookup);
    expect(request.request.body).toEqual({
      bookingReference: 'K7QM-3XPD-9RTV',
      email: 'juergen.mueller@example.de',
    });
    request.flush(null, { status: 202, statusText: 'Accepted' });
    TestBed.tick();

    // "Gonderildi" degil, "eslesme varsa gonderildi".
    expect(text(element, '[data-testid="lookup-sent"]')).toContain('manage.lookup.sentBody');
    expect(element.querySelector('[data-testid="lookup-privacy-note"]')).toBeNull();
  });

  it('eksik/gecersiz alanlarda istek atmaz', async () => {
    const element = await renderRoute('/de/manage');
    TestBed.tick();

    element.querySelector<HTMLButtonElement>('[data-testid="lookup-submit"]')?.click();
    TestBed.tick();

    http().expectNone(API.lookup);
    expect(element.querySelector('[data-testid="error-summary"]')).not.toBeNull();
  });
});

describe('Iptal — ucretsiz pencere', () => {
  it('ucretsiz oldugunu soyler ve `acknowledgedFeeAmount` GONDERMEZ', async () => {
    const element = await openBooking();
    click(element, 'cancel-open');

    expect(text(element, '[data-testid="cancel-fee-statement"]')).toContain('manage.cancel.free');
    expect(element.querySelector('[data-testid="check-feeAcknowledged"]')).toBeNull();

    click(element, 'cancel-confirm');
    const request = http().expectOne(API.cancel(TOKEN));
    expect(request.request.body).toEqual({ reason: null, acknowledgedFeeAmount: null });
    request.flush(booking({ status: 'Cancelled' }));
  });
});

describe('Iptal — ucretli pencere', () => {
  const chargeable = booking({
    cancellation: {
      ...CANCELLATION,
      isFreeCancellationAvailable: false,
      canCancelOnline: true,
      chargedFeeAmount: null,
    },
  });

  it('tutari gosterir ve TEYIT olmadan istek atmaz', async () => {
    const element = await openBooking(chargeable);
    click(element, 'cancel-open');

    expect(text(element, '[data-testid="cancel-fee-statement"]')).toContain('Gebuehr');
    expect(text(element, '[data-testid="cancel-fee-statement"]')).toContain('405,00');

    click(element, 'cancel-confirm');
    http().expectNone(API.cancel(TOKEN));
    expect(element.querySelector('[data-testid="error-feeAcknowledged"]')).not.toBeNull();
  });

  it('teyit verildiginde tutari acikca gonderir', async () => {
    const element = await openBooking(chargeable);
    click(element, 'cancel-open');

    const box = element.querySelector<HTMLInputElement>('[data-testid="check-feeAcknowledged"]');
    box!.checked = true;
    box!.dispatchEvent(new Event('change'));
    TestBed.tick();

    click(element, 'cancel-confirm');
    const request = http().expectOne(API.cancel(TOKEN));
    expect(request.request.body.acknowledgedFeeAmount).toBe(405);
    request.flush(booking({ status: 'Cancelled' }));
  });

  it('Kurtaxe`nin ucret matrahina girmedigini yazar', async () => {
    const element = await openBooking(chargeable);
    click(element, 'cancel-open');

    expect(element.querySelector('[data-testid="cancel-city-tax-note"]')).not.toBeNull();
  });

  it('sunucu tutari farkli hesaplarsa (409) guncel kaydi tazeler', async () => {
    const element = await openBooking(chargeable);
    click(element, 'cancel-open');

    const box = element.querySelector<HTMLInputElement>('[data-testid="check-feeAcknowledged"]');
    box!.checked = true;
    box!.dispatchEvent(new Event('change'));
    TestBed.tick();

    click(element, 'cancel-confirm');
    http()
      .expectOne(API.cancel(TOKEN))
      .flush(problem('FEE_ACKNOWLEDGEMENT_REQUIRED', { errors: { AcknowledgedFeeAmount: ['405.00'] } }), {
        status: 409,
        statusText: 'Conflict',
      });
    TestBed.tick();

    http().expectOne(API.booking(TOKEN)).flush(chargeable);
    TestBed.tick();

    expect(element.textContent).not.toContain('FEE_ACKNOWLEDGEMENT_REQUIRED');
  });
});

describe('Iptal — online mumkun olmayan durumlar', () => {
  it('konaklama basladiysa oteli aramayi onerir ve telefonu gosterir', async () => {
    const element = await openBooking(booking({ status: 'InHouse' }));

    expect(element.querySelector('[data-testid="cancel-unavailable"]')).not.toBeNull();
    expect(text(element, '[data-testid="cancel-phone"]')).toBe('+49 30 5550000');
    expect(element.querySelector('[data-testid="cancel-open"]')).toBeNull();
  });

  it('zaten iptalliyse iptal bolumu hic gosterilmez', async () => {
    const element = await openBooking(
      booking({
        status: 'Cancelled',
        cancellation: { ...CANCELLATION, canCancelOnline: false, chargedFeeAmount: 0 },
      }),
    );

    expect(element.querySelector('[data-testid="cancelled-notice"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="cancel-open"]')).toBeNull();
  });

  it('bulunamayan rezervasyonda ham kod yerine cikis yolu sunar', async () => {
    const element = await renderRoute(`/de/manage/${TOKEN}`);
    http()
      .expectOne(API.booking(TOKEN))
      .flush({ status: 404, extensions: { code: 'BOOKING_NOT_FOUND' } }, {
        status: 404,
        statusText: 'Not Found',
      });
    TestBed.tick();

    expect(text(element, '[data-testid="error-body"]')).toBe('errors.public.bookingNotFound');
    expect(element.textContent).not.toContain('BOOKING_NOT_FOUND');
  });
});
