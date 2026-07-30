import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import type { GuestResponse } from '../../core/models/guest.model';
import type { InvoiceDetailResponse, InvoiceStatus } from '../../core/models/invoice.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';
import { InvoiceFormPage } from './invoice-form';

const GUEST: GuestResponse = {
  id: 'g-1',
  firstName: 'Anna',
  lastName: 'Mueller',
  fullName: 'Anna Mueller',
  stayCount: null,
};

const GUEST_PAGE: PagedResult<GuestResponse> = {
  items: [GUEST],
  page: 1,
  pageSize: 20,
  totalCount: 1,
};

function invoice(status: InvoiceStatus): InvoiceDetailResponse {
  return {
    id: 'inv-1',
    invoiceNumber: status === 'Draft' ? null : '2026-000001',
    status,
    issuedAt: status === 'Draft' ? null : '2026-07-30T12:35:36Z',
    guestId: 'g-1',
    guestName: 'Anna Mueller',
    reservationId: null,
    reservationNumber: null,
    culture: 'de',
    currency: 'EUR',
    netAmount: 100,
    vatAmount: 19,
    cityTaxAmount: 0,
    grossAmount: 119,
    paidAmount: 0,
    outstandingAmount: 119,
    cancelledByInvoiceId: null,
    cancelsInvoiceId: null,
    isCancellationInvoice: false,
    createdAt: '2026-07-30T12:33:33Z',
    lineItems: [
      {
        id: 'li-1',
        type: 'Extra',
        description: 'Frühstück',
        quantity: 2,
        unitPrice: 12.5,
        vatRate: 19,
        lineNet: 21.01,
        lineVat: 3.99,
        lineGross: 25,
        serviceDate: '2026-07-20',
        sortOrder: 0,
      },
    ],
    payments: [],
    auditTrail: [],
  };
}

function user(): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'klaus.meier@hotel.de',
    roles: ['Manager'],
    permissions: [PERMISSIONS.InvoicesView, PERMISSIONS.InvoicesCreate],
    hotels: [{ id: 'h-1', name: 'Hotel Adler', currency: 'EUR' }],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
  };
}

/** Zoneless: `whenStable()` bekleyen promise'leri beklemez. */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('InvoiceFormPage — taslak olusturma/duzenleme', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'invoices/new', component: InvoiceFormPage },
          { path: 'invoices/:id/edit', component: InvoiceFormPage },
          { path: 'invoices/:id', component: InvoiceFormPage },
          { path: 'invoices', component: InvoiceFormPage },
        ]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  async function renderCreate(
    url = '/invoices/new',
  ): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    TestBed.inject(AuthStore).setSession(user());
    const harness = await RouterTestingHarness.create(url);
    http.expectOne((request) => request.url === `${baseUrl}/guests`).flush(GUEST_PAGE);
    await tick();
    harness.detectChanges();
    return { harness, element: harness.routeNativeElement as HTMLElement };
  }

  function setValue(element: HTMLElement, selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
    input!.value = value;
    input!.dispatchEvent(new Event('input'));
    input!.dispatchEvent(new Event('change'));
  }

  it('elle satirlarda vergi/toplam GONDERMEZ ve sayisal alanlari sayiya cevirir', async () => {
    const { harness, element } = await renderCreate();

    setValue(element, '[data-testid="invoice-guest-select"]', 'g-1');
    setValue(element, '#invoice-line-type-0', 'Extra');
    setValue(element, '#invoice-line-description-0', 'Übernachtung Doppelzimmer');
    // `type="number"` + `formControlName`: Angular kontrole SAYI yazar.
    setValue(element, '#invoice-line-quantity-0', '2');
    // Para alani metindir; de-DE virgullu yazim kabul edilir.
    setValue(element, '#invoice-line-price-0', '110,00');
    setValue(element, '#invoice-line-date-0', '2026-07-20');
    harness.detectChanges();

    element
      .querySelector<HTMLFormElement>('[data-testid="invoice-form"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/invoices`);
    expect(request.request.method).toBe('POST');

    const body = request.request.body as Record<string, unknown>;
    expect(body['guestId']).toBe('g-1');
    // Iki yol birbirini disler: elle yolda `reservationId` GONDERILMEZ.
    expect(body).not.toHaveProperty('reservationId');

    const lines = body['lineItems'] as Record<string, unknown>[];
    expect(lines).toHaveLength(1);
    expect(lines[0]).toEqual({
      type: 'Extra',
      description: 'Übernachtung Doppelzimmer',
      quantity: 2,
      unitPrice: 110,
      serviceDate: '2026-07-20',
    });
    expect(typeof lines[0]['quantity']).toBe('number');
    expect(typeof lines[0]['unitPrice']).toBe('number');
    // Vergi matrahi istemciden gelmez.
    expect(lines[0]).not.toHaveProperty('vatRate');
    expect(lines[0]).not.toHaveProperty('lineNet');
    expect(lines[0]).not.toHaveProperty('lineVat');
    expect(body).not.toHaveProperty('grossAmount');
  });

  it('rezervasyon yolunda yalnizca reservationId gonderir (lineItems yok)', async () => {
    const { harness, element } = await renderCreate('/invoices/new?reservationId=res-1');

    http.expectOne((request) => request.url === `${baseUrl}/reservations/res-1`).flush({
      id: 'res-1',
      reservationNumber: 'RES-2026-00001',
      status: 'CheckedOut',
      channel: 'Direct',
      roomId: 'r-1',
      roomNumber: '201',
      roomTypeId: 't-1',
      roomTypeCode: 'DBL',
      guestId: 'g-1',
      guestName: 'Anna Mueller',
      checkIn: '2026-08-01',
      checkOut: '2026-08-04',
      nights: 3,
      adults: 2,
      children: 0,
      totalAmount: 450,
      currency: 'EUR',
      depositPercent: 0,
      depositAmount: 0,
    });
    await tick();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="invoice-selected-reservation"]')?.textContent).toContain(
      'RES-2026-00001',
    );

    element
      .querySelector<HTMLFormElement>('[data-testid="invoice-form"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/invoices`);
    const body = request.request.body as Record<string, unknown>;
    expect(body['reservationId']).toBe('res-1');
    // Sozlesme: iki yol birbirini disler; `lineItems` gonderilirse 400 doner.
    expect(body).not.toHaveProperty('lineItems');
    expect(body).not.toHaveProperty('guestId');
  });

  it('Finalized faturanin duzenleme yolunu kilitler ve PUT gondermez', async () => {
    // Ikinci savunma hatti: detay ekrani baglantiyi zaten hic gostermez.
    TestBed.inject(AuthStore).setSession(user());
    const harness = await RouterTestingHarness.create('/invoices/inv-1/edit');
    http.expectOne((request) => request.url === `${baseUrl}/guests`).flush(GUEST_PAGE);
    http.expectOne((request) => request.url === `${baseUrl}/invoices/inv-1`).flush(
      invoice('Finalized'),
    );
    await tick();
    harness.detectChanges();

    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="invoice-form-locked"]')?.textContent).toContain(
      'invoices.form.notEditable',
    );
    expect(element.querySelector('[data-testid="invoice-form"]')).toBeNull();
    http.expectNone((request) => request.method === 'PUT');
  });

  it('taslak duzenlemesinde satirlari yukler ve PUT ile tamamen degistirir', async () => {
    TestBed.inject(AuthStore).setSession(user());
    const harness = await RouterTestingHarness.create('/invoices/inv-1/edit');
    http.expectOne((request) => request.url === `${baseUrl}/guests`).flush(GUEST_PAGE);
    http.expectOne((request) => request.url === `${baseUrl}/invoices/inv-1`).flush(invoice('Draft'));
    await tick();
    harness.detectChanges();

    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelectorAll('[data-testid="invoice-line-row"]')).toHaveLength(1);
    expect(element.querySelector<HTMLInputElement>('#invoice-line-description-0')?.value).toBe(
      'Frühstück',
    );

    setValue(element, '#invoice-line-price-0', '15');
    harness.detectChanges();

    element
      .querySelector<HTMLFormElement>('[data-testid="invoice-form"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/invoices/inv-1`);
    expect(request.request.method).toBe('PUT');
    const lines = (request.request.body as Record<string, unknown>)['lineItems'] as Record<
      string,
      unknown
    >[];
    expect(lines[0]).toMatchObject({ description: 'Frühstück', quantity: 2, unitPrice: 15 });
  });

  it('eksik satirda istek gondermez ve alan hatasi gosterir', async () => {
    const { harness, element } = await renderCreate();

    setValue(element, '[data-testid="invoice-guest-select"]', 'g-1');
    // Aciklama ve fiyat bos birakilir.
    element
      .querySelector<HTMLFormElement>('[data-testid="invoice-form"]')!
      .dispatchEvent(new Event('submit'));
    await tick();
    harness.detectChanges();

    http.expectNone((request) => request.url === `${baseUrl}/invoices`);
    expect(element.querySelector('[data-testid="invoice-form-error"]')?.textContent).toContain(
      'invoices.form.validation.lineRequired',
    );
    expect(
      element.querySelector('[data-testid="invoice-line-description-error"]')?.textContent,
    ).toContain('invoices.form.validation.descriptionRequired');
  });
});
