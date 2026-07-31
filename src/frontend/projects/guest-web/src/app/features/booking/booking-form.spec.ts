import { describe, expect, it } from 'vitest';

import { hold } from '../../../testing/public-fixtures';
import type { PublicGuestField } from '../../core/api/public-models';
import {
  emptyBookingForm,
  toCreateBookingRequest,
  validateBookingForm,
  type BookingFormValue,
} from './booking-form';

const FIELDS = new Set<PublicGuestField>([
  'firstName',
  'lastName',
  'email',
  'phone',
  'invoiceAddress',
  'estimatedArrivalLocalTime',
  'guestNote',
]);

const message = (key: string, params?: Record<string, unknown>) =>
  params ? `${key}:${JSON.stringify(params)}` : key;

function filled(overrides: Partial<BookingFormValue> = {}): BookingFormValue {
  return {
    ...emptyBookingForm(),
    firstName: 'Jürgen',
    lastName: 'Müller',
    email: 'juergen.mueller@example.de',
    termsAccepted: true,
    privacyAcknowledged: true,
    withdrawalAcknowledged: true,
    bookerIsAdult: true,
    ...overrides,
  };
}

function fields(problems: readonly { field: string }[]): string[] {
  return problems.map((problem) => problem.field);
}

describe('Form baslangic durumu — DSGVO Art. 4 Nr. 11', () => {
  it('HICBIR onay kutusu isaretli degildir', () => {
    const value = emptyBookingForm();

    expect(value.termsAccepted).toBe(false);
    expect(value.privacyAcknowledged).toBe(false);
    expect(value.withdrawalAcknowledged).toBe(false);
    expect(value.bookerIsAdult).toBe(false);
    expect(value.marketingOptIn).toBe(false);
  });
});

describe('Dogrulama — sozlesme §6.2 kurallarinin istemci karsiligi', () => {
  it('bos formda zorunlu alanlari ve zorunlu onaylari bildirir', () => {
    const problems = validateBookingForm(emptyBookingForm(), FIELDS, message);

    expect(fields(problems)).toEqual(
      expect.arrayContaining([
        'firstName',
        'lastName',
        'email',
        'termsAccepted',
        'privacyAcknowledged',
        'withdrawalAcknowledged',
        'bookerIsAdult',
      ]),
    );
  });

  it('eksiksiz formda hata uretmez', () => {
    expect(validateBookingForm(filled(), FIELDS, message)).toEqual([]);
  });

  it('pazarlama onayi ZORUNLU DEGILDIR (rezervasyon `false` ile tamamlanir)', () => {
    expect(validateBookingForm(filled({ marketingOptIn: false }), FIELDS, message)).toEqual([]);
  });

  it('gecersiz e-postayi yakalar', () => {
    const problems = validateBookingForm(filled({ email: 'juergen@' }), FIELDS, message);
    expect(fields(problems)).toEqual(['email']);
  });

  it('geliş saatini `HH:mm` bicimine zorlar', () => {
    const problems = validateBookingForm(
      filled({ estimatedArrivalLocalTime: '25:99' }),
      FIELDS,
      message,
    );
    expect(fields(problems)).toEqual(['estimatedArrivalLocalTime']);
  });

  it('misafir notunu 500 karakterle sinirlar', () => {
    const problems = validateBookingForm(
      filled({ guestNote: 'x'.repeat(501) }),
      FIELDS,
      message,
    );
    expect(fields(problems)).toEqual(['guestNote']);
  });

  it('fatura blogu ACILDIYSA adres zorunlu olur, kapaliyken degil', () => {
    expect(validateBookingForm(filled({ invoiceRequested: false }), FIELDS, message)).toEqual([]);

    const problems = validateBookingForm(filled({ invoiceRequested: true }), FIELDS, message);
    expect(fields(problems)).toContain('invoiceAddressLine');
  });

  it('sunucu bir alani ISTEMIYORSA o alan dogrulanmaz', () => {
    const withoutPhone = new Set<PublicGuestField>(['firstName', 'lastName', 'email']);
    const problems = validateBookingForm(
      filled({ phone: 'x'.repeat(80), guestNote: 'y'.repeat(900) }),
      withoutPhone,
      message,
    );

    expect(problems).toEqual([]);
  });
});

describe('Istek gövdesi — §312j kanit kaydi ve veri minimizasyonu', () => {
  it('hold`daki ozetin hash`ini AYNEN geri gonderir', () => {
    const source = hold();
    const request = toCreateBookingRequest(source, filled(), 'de', 'zahlungspflichtig buchen');

    expect(request.checkout.summaryHash).toBe(source.orderSummary.hash);
  });

  it('GOSTERILEN dugme metnini kaydeder (uydurmaz)', () => {
    const request = toCreateBookingRequest(hold(), filled(), 'de', 'kostenpflichtig bestellen');
    expect(request.checkout.orderButtonLabel).toBe('kostenpflichtig bestellen');
  });

  it('onaylanan hukuki versiyonlari hold`dan alir', () => {
    const request = toCreateBookingRequest(hold(), filled(), 'de', 'zahlungspflichtig buchen');

    expect(request.consents.termsVersion).toBe('2026-07-01');
    expect(request.consents.privacyNoticeVersion).toBe('2026-07-01');
    expect(request.consents.withdrawalNoticeVersion).toBe('2026-07-01');
  });

  it('MELDESCHEIN verisi gondermez: dogum tarihi, uyrukluk, kimlik yok', () => {
    const request = toCreateBookingRequest(hold(), filled(), 'de', 'zahlungspflichtig buchen');
    const flat = JSON.stringify(request).toLowerCase();

    for (const forbidden of ['birthdate', 'nationality', 'passport', 'idnumber', 'signature']) {
      expect(flat, forbidden).not.toContain(forbidden);
    }
    expect(request.guest.countryOfResidence).toBeNull();
  });

  it('KART ALANI icermez (PCI-DSS kapsam disiligi)', () => {
    const request = toCreateBookingRequest(hold(), filled(), 'de', 'zahlungspflichtig buchen');
    const flat = JSON.stringify(request).toLowerCase();

    for (const forbidden of ['cardnumber', 'pan', 'cvc', 'cvv', 'expirymonth', 'cardholder']) {
      expect(flat, forbidden).not.toContain(forbidden);
    }
    expect(request.payment.guarantee).toBeNull();
  });

  it('bos opsiyonel alanlari `null` gonderir (bos dize degil)', () => {
    const request = toCreateBookingRequest(hold(), filled(), 'de', 'zahlungspflichtig buchen');

    expect(request.guest.phone).toBeNull();
    expect(request.stay.guestNote).toBeNull();
    expect(request.stay.estimatedArrivalLocalTime).toBeNull();
    expect(request.invoiceAddress).toBeNull();
  });

  it('fatura blogu doldurulduysa gönderilir', () => {
    const request = toCreateBookingRequest(
      hold(),
      filled({
        invoiceRequested: true,
        invoiceCompany: 'Beispiel GmbH',
        invoiceAddressLine: 'Musterweg 3',
        invoicePostalCode: '10115',
        invoiceCity: 'Berlin',
        invoiceCountry: 'de',
      }),
      'de',
      'zahlungspflichtig buchen',
    );

    expect(request.invoiceAddress).toEqual({
      company: 'Beispiel GmbH',
      addressLine: 'Musterweg 3',
      postalCode: '10115',
      city: 'Berlin',
      country: 'DE',
      vatId: null,
    });
  });

  it('odeme yontemini hold`un sundugu secenekten alir', () => {
    const request = toCreateBookingRequest(hold(), filled(), 'de', 'zahlungspflichtig buchen');
    expect(request.payment.method).toBe('PayAtProperty');
  });
});
