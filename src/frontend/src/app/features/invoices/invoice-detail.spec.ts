import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import {
  canCancelInvoice,
  canFinalizeInvoice,
  canRecordPayment,
  isEditableInvoice,
  producesCancellationInvoice,
  type InvoiceDetailResponse,
  type InvoiceStatus,
} from '../../core/models/invoice.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';
import { InvoiceDetailPage } from './invoice-detail';

const ALL_PERMISSIONS: readonly PermissionKey[] = [
  PERMISSIONS.InvoicesView,
  PERMISSIONS.InvoicesCreate,
  PERMISSIONS.InvoicesApprove,
  PERMISSIONS.InvoicesCancel,
];

function invoice(
  status: InvoiceStatus,
  overrides: Partial<InvoiceDetailResponse> = {},
): InvoiceDetailResponse {
  return {
    id: 'inv-1',
    invoiceNumber: status === 'Draft' ? null : '2026-000001',
    status,
    issuedAt: status === 'Draft' ? null : '2026-07-30T12:35:36.481809+00:00',
    guestId: 'g-1',
    guestName: 'Anna Mueller',
    reservationId: 'res-1',
    reservationNumber: 'RES-2026-00001',
    culture: 'de',
    currency: 'EUR',
    netAmount: 399.49,
    vatAmount: 32.51,
    cityTaxAmount: 18,
    grossAmount: 450,
    paidAmount: 0,
    outstandingAmount: 450,
    cancelledByInvoiceId: null,
    cancelsInvoiceId: null,
    isCancellationInvoice: false,
    createdAt: '2026-07-30T12:33:33.7+00:00',
    lineItems: [
      {
        id: 'li-1',
        type: 'RoomCharge',
        description: 'Room charge 2026-08-01 - 2026-08-04',
        quantity: 3,
        unitPrice: 129,
        vatRate: 7,
        lineNet: 361.68,
        lineVat: 25.32,
        lineGross: 387,
        serviceDate: '2026-08-01',
        sortOrder: 0,
      },
      {
        id: 'li-2',
        type: 'CityTax',
        description: 'City tax (Kurtaxe)',
        quantity: 6,
        unitPrice: 3,
        vatRate: 0,
        lineNet: 18,
        lineVat: 0,
        lineGross: 18,
        serviceDate: '2026-08-01',
        sortOrder: 1,
      },
    ],
    payments: [],
    auditTrail: [
      {
        id: 'a-1',
        action: 'Created',
        performedByUserId: 'u-1',
        performedAt: '2026-07-30T12:33:33.781674+00:00',
        details: '{"source":"reservation","grossAmount":450.00}',
      },
    ],
    ...overrides,
  };
}

function user(permissions: readonly PermissionKey[]): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'klaus.meier@hotel.de',
    roles: ['Manager'],
    permissions,
    hotels: [{ id: 'h-1', name: 'Hotel Adler', currency: 'EUR' }],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
  };
}

/** Zoneless: `whenStable()` bekleyen promise'leri beklemez. */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('InvoiceDetailPage', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'invoices/:id', component: InvoiceDetailPage },
          { path: 'invoices', component: InvoiceDetailPage },
          { path: 'reservations/:id', component: InvoiceDetailPage },
        ]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  async function render(
    status: InvoiceStatus,
    permissions: readonly PermissionKey[] = ALL_PERMISSIONS,
    overrides: Partial<InvoiceDetailResponse> = {},
  ): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const harness = await RouterTestingHarness.create('/invoices/inv-1');
    http
      .expectOne((request) => request.url === `${baseUrl}/invoices/inv-1`)
      .flush(invoice(status, overrides));
    await tick();
    harness.detectChanges();

    return { harness, element: harness.routeNativeElement as HTMLElement };
  }

  it('Finalized faturada duzenleme aksiyonunu HIC render etmez', async () => {
    // GoBD §6.1: kesinlesmis fatura degistirilemez; yasak yolu gostermeyiz.
    const { element } = await render('Finalized');

    expect(element.querySelector('[data-testid="invoice-edit"]')).toBeNull();
    expect(element.querySelector('[data-testid="invoice-finalize"]')).toBeNull();
    // Odeme ve iptal ise mumkundur.
    expect(element.querySelector('[data-testid="invoice-add-payment"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="invoice-cancel"]')).not.toBeNull();
  });

  it('Draft faturada duzenleme ve kesinlestirme sunar, odeme sunmaz', async () => {
    const { element } = await render('Draft');

    expect(element.querySelector('[data-testid="invoice-edit"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="invoice-finalize"]')).not.toBeNull();
    // Odeme yalnizca Finalized faturaya kaydedilir (taslakta 409 verirdi).
    expect(element.querySelector('[data-testid="invoice-add-payment"]')).toBeNull();
    // Taslakta numara yoktur; bos hucre yerine acik metin.
    expect(element.querySelector('[data-testid="invoice-detail-no-number"]')?.textContent).toContain(
      'invoices.noNumber',
    );
  });

  it('taslak iptalinde onay metni storno URETILMEYECEGINI soyler', async () => {
    const { harness, element } = await render('Draft');

    element.querySelector<HTMLButtonElement>('[data-testid="invoice-cancel"] button')!.click();
    harness.detectChanges();

    expect(
      element.querySelector('[data-testid="invoice-cancel-confirm-text"]')?.textContent,
    ).toContain('invoices.cancel.confirmDraft');
  });

  it('kesinlesmis faturada iptal onayi Stornorechnung uretilecegini soyler', async () => {
    const { harness, element } = await render('Finalized');

    element.querySelector<HTMLButtonElement>('[data-testid="invoice-cancel"] button')!.click();
    harness.detectChanges();

    expect(
      element.querySelector('[data-testid="invoice-cancel-confirm-text"]')?.textContent,
    ).toContain('invoices.cancel.confirmStorno');

    element
      .querySelector<HTMLButtonElement>('[data-testid="invoice-cancel-confirm"] button')!
      .click();
    await tick();

    const request = http.expectOne(
      (candidate) => candidate.url === `${baseUrl}/invoices/inv-1/cancel`,
    );
    expect(request.request.method).toBe('POST');
    request.flush(
      invoice('Cancelled', { cancelledByInvoiceId: 'inv-2' }),
    );
    await tick();
    harness.detectChanges();

    // Storno'ya karsilikli baglanti gosterilir.
    expect(element.querySelector('[data-testid="invoice-storno-link"]')).not.toBeNull();
  });

  it('fazla odemenin 409 yanitini tutar ALANINA baglar', async () => {
    const { harness, element } = await render('Finalized');

    element.querySelector<HTMLButtonElement>('[data-testid="invoice-add-payment"] button')!.click();
    harness.detectChanges();

    const amount = element.querySelector<HTMLInputElement>('#invoice-payment-amount')!;
    amount.value = '500';
    amount.dispatchEvent(new Event('input'));
    harness.detectChanges();

    element
      .querySelector<HTMLFormElement>('[data-testid="invoice-payment-panel"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne(
      (candidate) => candidate.url === `${baseUrl}/invoices/inv-1/payments`,
    );
    expect(request.request.body).toMatchObject({ method: 'Card', amount: 500 });
    expect(typeof (request.request.body as Record<string, unknown>)['amount']).toBe('number');

    request.flush(
      {
        status: 409,
        title: 'Islem mevcut durumla celisiyor.',
        detail: 'Odeme tutari faturanin acik bakiyesini asiyor (acik: 450,00 EUR).',
      },
      { status: 409, statusText: 'Conflict' },
    );
    await tick();
    harness.detectChanges();

    // Genel serit degil: hata duzeltilecek alanin yaninda gorunur.
    expect(
      element.querySelector('[data-testid="invoice-payment-amount-error"]')?.textContent,
    ).toContain('invoices.payment.overpayment');
    expect(amount.getAttribute('aria-invalid')).toBe('true');
    // Panel acik kalir ki kullanici tutari duzeltebilsin.
    expect(element.querySelector('[data-testid="invoice-payment-panel"]')).not.toBeNull();
  });

  it('kalan bakiye kapandiginda Paid durumunu ve sifir bakiyeyi gosterir', async () => {
    const { harness, element } = await render('Finalized');

    element.querySelector<HTMLButtonElement>('[data-testid="invoice-add-payment"] button')!.click();
    harness.detectChanges();
    element
      .querySelector<HTMLFormElement>('[data-testid="invoice-payment-panel"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne(
      (candidate) => candidate.url === `${baseUrl}/invoices/inv-1/payments`,
    );
    // Kalan bakiye on-doldurulmustur.
    expect(request.request.body).toMatchObject({ amount: 450 });
    request.flush(
      invoice('Paid', {
        paidAmount: 450,
        outstandingAmount: 0,
        payments: [
          {
            id: 'p-1',
            method: 'Card',
            amount: 450,
            paidAt: '2026-07-30T12:36:00+00:00',
            reference: 'TERM-4711',
          },
        ],
      }),
    );
    await tick();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="invoice-detail-outstanding"]')?.textContent).toContain(
      '0,00',
    );
    expect(element.querySelectorAll('[data-testid="invoice-payment"]')).toHaveLength(1);
    // Odenmis faturaya ikinci odeme sunulmaz (sunucu 409 verirdi).
    expect(element.querySelector('[data-testid="invoice-add-payment"]')).toBeNull();
  });

  it('Invoices.Approve izni olmayan kullaniciya kesinlestirme dugmesini gostermez', async () => {
    const { element } = await render('Draft', [PERMISSIONS.InvoicesView, PERMISSIONS.InvoicesCreate]);

    expect(element.querySelector('[data-testid="invoice-finalize"]')).toBeNull();
    // Iptal de `Invoices.Cancel` gerektirir.
    expect(element.querySelector('[data-testid="invoice-cancel"]')).toBeNull();
    // Duzenleme `Invoices.Create` ile gorunur.
    expect(element.querySelector('[data-testid="invoice-edit"]')).not.toBeNull();
  });

  it('PDF dugmesini devre disi gosterir ve sahte indirme yapmaz (501)', async () => {
    const { element } = await render('Finalized');

    const pdf = element.querySelector<HTMLButtonElement>('[data-testid="invoice-pdf"]');
    expect(pdf).not.toBeNull();
    expect(pdf!.disabled).toBe(true);
    expect(element.textContent).toContain('invoices.pdfUnavailable');
    // Ekran acilirken PDF ucuna istek gitmez.
    http.expectNone((request) => request.url.endsWith('/pdf'));
  });

  it('KDV kirilimini ve Kurtaxe yi AYRI gosterir, denetim izini listeler', async () => {
    const { element } = await render('Finalized');

    const totals = element.querySelector('[data-testid="invoice-totals"]')?.textContent ?? '';
    expect(totals).toContain('399,49');
    expect(totals).toContain('32,51');
    expect(element.querySelector('[data-testid="invoice-city-tax"]')?.textContent).toContain('18,00');
    expect(element.querySelector('[data-testid="invoice-detail-gross"]')?.textContent).toContain(
      '450,00',
    );

    const audit = element.querySelectorAll('[data-testid="invoice-audit-entry"]');
    expect(audit).toHaveLength(1);
    expect(audit[0].textContent).toContain('invoices.audit.action.created');
  });

  it('storno faturasinda orijinale karsilikli baglanti kurar', async () => {
    const { element } = await render('Finalized', ALL_PERMISSIONS, {
      isCancellationInvoice: true,
      cancelsInvoiceId: 'inv-0',
      grossAmount: -450,
      outstandingAmount: -450,
    });

    expect(element.querySelector('[data-testid="invoice-cancels-link"]')).not.toBeNull();
    // Brut tutari <= 0 olan belgeye odeme kaydedilemez.
    expect(element.querySelector('[data-testid="invoice-add-payment"]')).toBeNull();
  });
});

describe('Fatura durum makinesi — istemci ile sunucu birebir ayni', () => {
  it('yalnizca taslagi duzenlenebilir/kesinlestirilebilir sayar', () => {
    expect(isEditableInvoice('Draft')).toBe(true);
    expect(isEditableInvoice('Finalized')).toBe(false);
    expect(isEditableInvoice('Paid')).toBe(false);
    expect(isEditableInvoice('Cancelled')).toBe(false);

    expect(canFinalizeInvoice('Draft')).toBe(true);
    expect(canFinalizeInvoice('Finalized')).toBe(false);
  });

  it('iptal ve storno dallanmasini sozlesmeye gore ayirir', () => {
    // Taslak iptalinde storno URETILMEZ; kesinlesmis/odenmis faturada uretilir.
    expect(producesCancellationInvoice('Draft')).toBe(false);
    expect(producesCancellationInvoice('Finalized')).toBe(true);
    expect(producesCancellationInvoice('Paid')).toBe(true);

    expect(canCancelInvoice('Draft')).toBe(true);
    expect(canCancelInvoice('Paid')).toBe(true);
    // Zaten iptal edilmis fatura ikinci kez iptal edilemez.
    expect(canCancelInvoice('Cancelled')).toBe(false);
  });

  it('odemeyi yalnizca pozitif tutarli Finalized faturada mumkun sayar', () => {
    expect(canRecordPayment({ status: 'Finalized', grossAmount: 450 })).toBe(true);
    expect(canRecordPayment({ status: 'Draft', grossAmount: 450 })).toBe(false);
    expect(canRecordPayment({ status: 'Paid', grossAmount: 450 })).toBe(false);
    expect(canRecordPayment({ status: 'Cancelled', grossAmount: 450 })).toBe(false);
    // Stornorechnung: iade akisi bu fazda yok.
    expect(canRecordPayment({ status: 'Finalized', grossAmount: -450 })).toBe(false);
  });
});
