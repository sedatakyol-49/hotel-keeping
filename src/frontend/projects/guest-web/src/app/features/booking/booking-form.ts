import { computed, signal } from '@angular/core';

import type {
  PublicCreateBookingRequest,
  PublicGuestField,
  PublicHold,
} from '../../core/api/public-models';
import type { FieldProblem } from '../../shared/ui/form/error-summary';

/**
 * ===========================================================================
 * REZERVASYON FORMU — durum + dogrulama (bilesenden AYRI)
 * ===========================================================================
 *
 * Neden ayri dosya: bu mantik hukuki kurallarin yogunlastigi yerdir
 * (hangi alan sorulur, hangi onay zorunludur, istek gövdesine ne konur) ve
 * bir sablonun icinde gomulu kalmamalidir. Ayri oldugu icin **sablonsuz** test
 * edilebilir; "form dogru istegi uretiyor mu" sorusu DOM'a bakmadan yanitlanir.
 *
 * VERI MINIMIZASYONU (DSGVO Art. 5 Abs. 1 lit. c):
 * Gorunur alanlar **sunucunun** bildirdigi listelerden turer
 * (`requiredGuestFields` / `optionalGuestFields`). Istemci kendi basina bir alan
 * ekleyemez; dolayisiyla dogum tarihi, uyrukluk, kimlik numarasi veya ev adresi
 * sorulmasi ancak sunucu sozlesmesi degisirse mumkun olur. Bu veriler
 * Meldeschein kapsamindadir (BMG §§29-30) ve **girişte** alinir.
 *
 * KART VERISI: hicbir alan yoktur, olmayacaktir (mimari §6.2). Sunucu tarafinda
 * ayrica bir tripwire vardir; istemci tarafinda korunma "alan uretmemektir".
 */

export interface BookingFormValue {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  estimatedArrivalLocalTime: string;
  guestNote: string;
  invoiceRequested: boolean;
  invoiceCompany: string;
  invoiceAddressLine: string;
  invoicePostalCode: string;
  invoiceCity: string;
  invoiceCountry: string;
  invoiceVatId: string;
  termsAccepted: boolean;
  privacyAcknowledged: boolean;
  withdrawalAcknowledged: boolean;
  bookerIsAdult: boolean;
  marketingOptIn: boolean;
}

/** Baslangic degerleri: **hicbir onay kutusu isaretli degildir**. */
export function emptyBookingForm(): BookingFormValue {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    estimatedArrivalLocalTime: '',
    guestNote: '',
    invoiceRequested: false,
    invoiceCompany: '',
    invoiceAddressLine: '',
    invoicePostalCode: '',
    invoiceCity: '',
    invoiceCountry: '',
    invoiceVatId: '',
    termsAccepted: false,
    privacyAcknowledged: false,
    withdrawalAcknowledged: false,
    bookerIsAdult: false,
    marketingOptIn: false,
  };
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
const TIME = /^([01]\d|2[0-3]):[0-5]\d$/;

/**
 * Dogrulama — sozlesme §6.2 kurallarinin **istemci karsiligi**.
 * Sunucu kurallari yine sunucudadir; buradaki amac agı bosuna mesgul etmemek
 * ve hatayi alanin yaninda gostermektir.
 */
export function validateBookingForm(
  value: BookingFormValue,
  fields: ReadonlySet<PublicGuestField>,
  message: (key: string, params?: Record<string, unknown>) => string,
): readonly FieldProblem[] {
  const problems: FieldProblem[] = [];

  const require = (field: string, text: string, ok: boolean, kind?: 'input' | 'check') => {
    if (!ok) {
      problems.push({ field, message: text, kind });
    }
  };

  if (fields.has('firstName')) {
    require('firstName', message('form.errors.required'), value.firstName.trim().length > 0);
    require(
      'firstName',
      message('form.errors.maxLength', { max: 100 }),
      value.firstName.length <= 100,
    );
  }
  if (fields.has('lastName')) {
    require('lastName', message('form.errors.required'), value.lastName.trim().length > 0);
    require(
      'lastName',
      message('form.errors.maxLength', { max: 100 }),
      value.lastName.length <= 100,
    );
  }
  if (fields.has('email')) {
    require('email', message('form.errors.email'), EMAIL.test(value.email.trim()));
    require('email', message('form.errors.maxLength', { max: 256 }), value.email.length <= 256);
  }
  if (fields.has('phone') && value.phone.length > 0) {
    require('phone', message('form.errors.maxLength', { max: 32 }), value.phone.length <= 32);
  }
  if (
    fields.has('estimatedArrivalLocalTime') &&
    value.estimatedArrivalLocalTime.length > 0 &&
    !TIME.test(value.estimatedArrivalLocalTime)
  ) {
    problems.push({
      field: 'estimatedArrivalLocalTime',
      message: message('form.errors.time'),
    });
  }
  if (fields.has('guestNote') && value.guestNote.length > 500) {
    problems.push({ field: 'guestNote', message: message('form.errors.maxLength', { max: 500 }) });
  }

  if (fields.has('invoiceAddress') && value.invoiceRequested) {
    require(
      'invoiceAddressLine',
      message('form.errors.required'),
      value.invoiceAddressLine.trim().length > 0,
    );
    require(
      'invoiceAddressLine',
      message('form.errors.maxLength', { max: 256 }),
      value.invoiceAddressLine.length <= 256,
    );
    require(
      'invoicePostalCode',
      message('form.errors.maxLength', { max: 16 }),
      value.invoicePostalCode.length <= 16,
    );
    require(
      'invoiceCity',
      message('form.errors.maxLength', { max: 100 }),
      value.invoiceCity.length <= 100,
    );
    require(
      'invoiceCompany',
      message('form.errors.maxLength', { max: 200 }),
      value.invoiceCompany.length <= 200,
    );
    require(
      'invoiceVatId',
      message('form.errors.maxLength', { max: 32 }),
      value.invoiceVatId.length <= 32,
    );
  }

  /*
   * Zorunlu onaylar. Sozlesme bunlarin `true` olmasini sart kosar; `false`
   * gonderilirse 400 doner. Istemci tarafinda ayri ayri gosterilir ki
   * kullanici hangi onayi vermedigini gorsun.
   */
  require('termsAccepted', message('form.errors.consentRequired'), value.termsAccepted, 'check');
  require(
    'privacyAcknowledged',
    message('form.errors.consentRequired'),
    value.privacyAcknowledged,
    'check',
  );
  require(
    'withdrawalAcknowledged',
    message('form.errors.consentRequired'),
    value.withdrawalAcknowledged,
    'check',
  );
  require('bookerIsAdult', message('form.errors.adultRequired'), value.bookerIsAdult, 'check');

  return problems;
}

/**
 * Istek gövdesini uretir.
 *
 * ONEMLI: `checkout.summaryHash` ve `checkout.orderButtonLabel` **gosterilen**
 * degerlerden gelir — hash hold yanitindan aynen, etiket ise dugmenin uzerinde
 * o an yazan metinden. Ikisi de §312j kanit kaydidir; uydurulmus bir deger
 * kaydi degersiz kilar.
 */
export function toCreateBookingRequest(
  hold: PublicHold,
  value: BookingFormValue,
  culture: string,
  orderButtonLabel: string,
): PublicCreateBookingRequest {
  const trimmed = (text: string) => (text.trim().length > 0 ? text.trim() : null);

  return {
    holdToken: hold.holdToken,
    checkout: {
      summaryHash: hold.orderSummary.hash,
      orderButtonLabel,
    },
    guest: {
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      email: value.email.trim(),
      phone: trimmed(value.phone),
      culture,
      /* Sunucu istemedigi surece sorulmaz ve gonderilmez. */
      countryOfResidence: null,
    },
    invoiceAddress: value.invoiceRequested
      ? {
          company: trimmed(value.invoiceCompany),
          addressLine: value.invoiceAddressLine.trim(),
          postalCode: value.invoicePostalCode.trim(),
          city: value.invoiceCity.trim(),
          country: value.invoiceCountry.trim().toUpperCase(),
          vatId: trimmed(value.invoiceVatId),
        }
      : null,
    stay: {
      estimatedArrivalLocalTime: trimmed(value.estimatedArrivalLocalTime),
      guestNote: trimmed(value.guestNote),
    },
    payment: { method: hold.paymentOptions[0]?.method ?? 'PayAtProperty', guarantee: null },
    consents: {
      termsAccepted: value.termsAccepted,
      termsVersion: hold.legal.terms.version,
      privacyNoticeAcknowledged: value.privacyAcknowledged,
      privacyNoticeVersion: hold.legal.privacyNotice.version,
      withdrawalNoticeAcknowledged: value.withdrawalAcknowledged,
      withdrawalNoticeVersion: hold.legal.withdrawalRight.noticeVersion,
      bookerIsAdult: value.bookerIsAdult,
      marketingOptIn: value.marketingOptIn,
    },
    challengeToken: null,
  };
}

/** Bilesende kullanilan kucuk yardimci: alan bazli hata mesajini bulur. */
export function problemFor(
  problems: readonly FieldProblem[],
  field: string,
): string | null {
  return problems.find((problem) => problem.field === field)?.message ?? null;
}

/**
 * Form durumu icin signal sarmalayici (bilesen bunu kullanir).
 *
 * DOGRULAMA ZAMANLAMASI (WCAG 3.3.1 uygulamasi):
 *  - Ilk gonderime kadar **hicbir hata gosterilmez**: kullanici daha yazmaya
 *    baslarken kirmizi uyari almaz.
 *  - Ilk gonderimden sonra **her degisiklikte yeniden dogrulanir**: duzeltilen
 *    alanin hatasi aninda kaybolur. Aksi halde ekranda "doldurulmus ama hala
 *    hatali gorunen" alanlar kalir ve hata ozeti yalan soyler.
 */
export function bookingFormState(
  validate: (value: BookingFormValue) => readonly FieldProblem[],
) {
  const value = signal<BookingFormValue>(emptyBookingForm());
  const problems = signal<readonly FieldProblem[]>([]);
  const submitted = signal(false);

  return {
    value: value.asReadonly(),
    problems: problems.asReadonly(),
    hasProblems: computed(() => problems().length > 0),

    patch(change: Partial<BookingFormValue>): void {
      const next = { ...value(), ...change };
      value.set(next);
      if (submitted()) {
        problems.set(validate(next));
      }
    },

    /** Gonderim denemesi: dogrular, sonucu saklar ve gecerli olup olmadigini bildirir. */
    submit(): boolean {
      submitted.set(true);
      const found = validate(value());
      problems.set(found);
      return found.length === 0;
    },

    reset(): void {
      submitted.set(false);
      problems.set([]);
      value.set(emptyBookingForm());
    },
  };
}
