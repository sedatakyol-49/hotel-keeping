import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { API, hold, problem } from '../../../testing/public-fixtures';
import { configureGuestTestBed, renderRoute } from '../../../testing/guest-test-bed';
import type { PublicHold } from '../../core/api/public-models';

const TOKEN = 'Vb3nQ8sT1kR6yPz0LmXhAw';
const URL = `/de/booking?holdToken=${TOKEN}`;

function http(): HttpTestingController {
  return TestBed.inject(HttpTestingController);
}

/** Sayfayi acar ve hold yanitini verir. */
async function open(response: PublicHold = hold()): Promise<HTMLElement> {
  const element = await renderRoute(URL);
  http().expectOne(API.hold(TOKEN)).flush(response);
  TestBed.tick();
  return element;
}

function type(element: HTMLElement, name: string, value: string): void {
  const input = element.querySelector<HTMLInputElement>(`[data-testid="field-${name}"]`);
  if (input === null) {
    throw new Error(`Alan bulunamadi: ${name}`);
  }
  input.value = value;
  input.dispatchEvent(new Event('input'));
  TestBed.tick();
}

function check(element: HTMLElement, name: string): void {
  const box = element.querySelector<HTMLInputElement>(`[data-testid="check-${name}"]`);
  if (box === null) {
    throw new Error(`Onay kutusu bulunamadi: ${name}`);
  }
  box.checked = true;
  box.dispatchEvent(new Event('change'));
  TestBed.tick();
}

function fillValidForm(element: HTMLElement): void {
  type(element, 'firstName', 'Jürgen');
  type(element, 'lastName', 'Müller');
  type(element, 'email', 'juergen.mueller@example.de');
  check(element, 'termsAccepted');
  check(element, 'privacyAcknowledged');
  check(element, 'withdrawalAcknowledged');
  check(element, 'bookerIsAdult');
}

function clickOrderButton(element: HTMLElement): void {
  element.querySelector<HTMLButtonElement>('[data-testid="order-button"]')?.click();
  TestBed.tick();
}

beforeEach(() => {
  /*
   * Oturum deposu jsdom'da dosya boyunca PAYLASILIR. Temizlenmezse bir onceki
   * testin hold token'i "adreste token yok" senaryosunu bozar — ki bu tam da
   * uygulamanin kurtarma davranisidir (token yoksa depodan bak).
   */
  globalThis.sessionStorage?.clear();
  configureGuestTestBed();
});

describe('Rezervasyon ekrani — veri minimizasyonu', () => {
  it('YALNIZCA sunucunun bildirdigi alanlari gosterir', async () => {
    const element = await open();

    for (const field of ['firstName', 'lastName', 'email', 'phone', 'guestNote']) {
      expect(element.querySelector(`[data-testid="field-${field}"]`), field).not.toBeNull();
    }
  });

  it('sunucu bir alani listelemezse o alan HIC cizilmez', async () => {
    const element = await open(
      hold({ optionalGuestFields: ['phone'] }), // not/geliş saati/fatura yok
    );

    expect(element.querySelector('[data-testid="field-guestNote"]')).toBeNull();
    expect(element.querySelector('[data-testid="field-estimatedArrivalLocalTime"]')).toBeNull();
    expect(element.querySelector('[data-testid="check-invoiceRequested"]')).toBeNull();
  });

  it('Meldeschein verisi (dogum tarihi, uyrukluk, kimlik) SORULMAZ', async () => {
    const element = await open();
    const html = element.innerHTML.toLowerCase();

    for (const forbidden of ['birthdate', 'nationality', 'passport', 'idnumber']) {
      expect(html, forbidden).not.toContain(`field-${forbidden}`);
    }
    expect(element.querySelector('[data-testid="minimization-note"]')).not.toBeNull();
  });

  it('KART ALANI icermez ve bunu acikca yazar', async () => {
    const element = await open();
    const inputs = Array.from(element.querySelectorAll('input'));

    for (const input of inputs) {
      const name = (input.getAttribute('name') ?? '').toLowerCase();
      expect(name).not.toMatch(/card|pan|cvc|cvv|expiry/u);
      expect(input.getAttribute('autocomplete') ?? '').not.toContain('cc-');
    }
    expect(element.querySelector('[data-testid="payment-no-card"]')).not.toBeNull();
  });

  it('hicbir onay kutusu ON ISARETLI degildir', async () => {
    const element = await open();
    const boxes = Array.from(element.querySelectorAll<HTMLInputElement>('input[type="checkbox"]'));

    expect(boxes.length).toBeGreaterThan(0);
    for (const box of boxes) {
      expect(box.checked, box.getAttribute('data-testid') ?? '').toBe(false);
    }
  });
});

describe('Rezervasyon ekrani — §312j', () => {
  it('siparis dugmesinin metni SUNUCUDAN gelir', async () => {
    const element = await open(
      hold({
        legal: {
          ...hold().legal,
          orderButton: {
            labelKey: 'legal.orderButton.payable',
            labelDe: 'jetzt zahlungspflichtig buchen',
            mustBeExactLabel: true,
          },
        },
      }),
    );

    expect(
      element.querySelector('[data-testid="order-button"]')?.textContent?.trim(),
    ).toBe('jetzt zahlungspflichtig buchen');
  });

  it('zorunlu ozet dugmenin HEMEN USTUNDEDIR', async () => {
    const element = await open();
    const summary = element.querySelector('[data-testid="order-summary"]');

    expect(summary?.nextElementSibling?.getAttribute('data-testid')).toBe('order-button');
  });

  it('cayma hakki bildirimi ve onay kutusu formda yer alir', async () => {
    const element = await open();

    expect(element.querySelector('[data-testid="withdrawal-excluded"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="check-withdrawalAcknowledged"]')).not.toBeNull();
  });

  it('gonderilen istek ozet hash`ini ve GOSTERILEN dugme metnini tasir', async () => {
    const element = await open();
    fillValidForm(element);
    clickOrderButton(element);

    const request = http().expectOne(API.bookings);
    expect(request.request.body.checkout).toEqual({
      summaryHash: hold().orderSummary.hash,
      orderButtonLabel: 'zahlungspflichtig buchen',
    });
    request.flush(null, { status: 500, statusText: 'Server Error' });
  });
});

describe('Rezervasyon ekrani — form dogrulama', () => {
  it('eksik formda istek ATILMAZ ve hata ozeti gosterilir', async () => {
    const element = await open();
    clickOrderButton(element);

    http().expectNone(API.bookings);
    expect(element.querySelector('[data-testid="error-summary"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="error-email"]')).not.toBeNull();
  });

  it('ILK gonderime kadar hata gostermez, sonra CANLI dogrular', async () => {
    const element = await open();

    // Yazarken kirmizi uyari yok.
    type(element, 'firstName', 'J');
    expect(element.querySelector('[data-testid="error-summary"]')).toBeNull();

    clickOrderButton(element);
    expect(element.querySelector('[data-testid="error-email"]')).not.toBeNull();

    // Duzeltilen alanin hatasi ANINDA kaybolur (ekranda yalan kalmaz).
    type(element, 'email', 'juergen.mueller@example.de');
    expect(element.querySelector('[data-testid="error-email"]')).toBeNull();
    expect(element.querySelector('[data-testid="error-summary"]')).not.toBeNull();
  });

  it('onay kutulari isaretlenmeden gonderim engellenir', async () => {
    const element = await open();
    type(element, 'firstName', 'Jürgen');
    type(element, 'lastName', 'Müller');
    type(element, 'email', 'juergen.mueller@example.de');
    clickOrderButton(element);

    http().expectNone(API.bookings);
    expect(element.querySelector('[data-testid="error-termsAccepted"]')).not.toBeNull();
  });
});

describe('Rezervasyon ekrani — hata ve kurtarma', () => {
  it('409 SUMMARY_CHANGED akisi DURDURUR ve yeniden onay ister', async () => {
    const element = await open();
    fillValidForm(element);
    clickOrderButton(element);

    http()
      .expectOne(API.bookings)
      .flush(problem('SUMMARY_CHANGED'), { status: 409, statusText: 'Conflict' });
    TestBed.tick();

    expect(element.querySelector('[data-testid="summary-changed"]')).not.toBeNull();
    expect(
      element.querySelector<HTMLButtonElement>('[data-testid="order-button"]')?.disabled,
    ).toBe(true);
  });

  it('yeniden onay istendiginde donmus teklif TAZELENIR', async () => {
    const element = await open();
    fillValidForm(element);
    clickOrderButton(element);
    http()
      .expectOne(API.bookings)
      .flush(problem('SUMMARY_CHANGED'), { status: 409, statusText: 'Conflict' });
    TestBed.tick();

    element.querySelector<HTMLButtonElement>('[data-testid="summary-reconfirm"]')?.click();
    TestBed.tick();

    http().expectOne(API.hold(TOKEN));
  });

  it('ham hata kodunu EKRANA yazmaz', async () => {
    const element = await open();
    fillValidForm(element);
    clickOrderButton(element);
    http()
      .expectOne(API.bookings)
      .flush(problem('SUMMARY_CHANGED'), { status: 409, statusText: 'Conflict' });
    TestBed.tick();

    expect(element.textContent).not.toContain('SUMMARY_CHANGED');
    expect(element.textContent).not.toContain('409');
  });

  it('hold okunamazsa kullaniciya cikis yolu sunar', async () => {
    const element = await renderRoute(URL);
    http()
      .expectOne(API.hold(TOKEN))
      .flush(problem('HOLD_EXPIRED'), { status: 409, statusText: 'Conflict' });
    TestBed.tick();

    const panel = element.querySelector('[data-error-code]');
    expect(panel?.getAttribute('data-error-code')).toBe('HOLD_EXPIRED');
    expect(element.querySelector('[data-testid="error-action"]')).not.toBeNull();
  });

  it('token yoksa arama adimina yonlendiren bir aciklama gosterir', async () => {
    const element = await renderRoute('/de/booking');
    TestBed.tick();

    expect(element.querySelector('[data-testid="booking-no-hold"]')).not.toBeNull();
  });
});

describe('Rezervasyon ekrani — hold sayaci', () => {
  it('kalan sureyi `role="timer"` ile, surekli okunmayan bicimde gosterir', async () => {
    const element = await open();
    const timer = element.querySelector('[data-testid="hold-timer-value"]');

    expect(timer?.getAttribute('role')).toBe('timer');
    expect(timer?.getAttribute('aria-live')).toBe('off');
    expect(timer?.getAttribute('aria-label')).toBeTruthy();
  });

  it('sure dolunca ne olacagini ONCEDEN soyler', async () => {
    const element = await open();
    expect(element.querySelector('[data-testid="hold-timer-hint"]')?.textContent?.trim()).toBe(
      'hold.hint',
    );
  });
});
