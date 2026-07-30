import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import {
  canCancel,
  canCheckIn,
  canCheckOut,
  canMarkNoShow,
  type FolioResponse,
  type ReservationResponse,
  type ReservationStatus,
} from '../../core/models/reservation.model';
import { AuthStore } from '../../core/state/auth.store';
import { ReservationDetailPage } from './reservation-detail';

function reservation(status: ReservationStatus): ReservationResponse {
  return {
    id: 'res-1',
    reservationNumber: 'RES-2026-00001',
    status,
    channel: 'Direct',
    roomId: 'r-1',
    roomNumber: '201',
    roomTypeId: 't-1',
    roomTypeCode: 'DBL',
    guestId: 'g-1',
    guestName: 'Jürgen Müller',
    guestEmail: 'juergen.mueller@example.de',
    checkIn: '2026-08-09',
    checkOut: '2026-08-12',
    nights: 3,
    adults: 2,
    children: 0,
    totalAmount: 450,
    currency: 'EUR',
    depositPercent: 20,
    depositAmount: 90,
    ratePlanId: null,
    ratePlanName: null,
    notes: null,
    folioId: 'f-1',
  };
}

const FOLIO: FolioResponse = {
  reservationId: 'res-1',
  reservationNumber: 'RES-2026-00001',
  folioId: 'f-1',
  isClosed: false,
  currency: 'EUR',
  guestName: 'Jürgen Müller',
  lines: [
    {
      id: 'l-1',
      type: 'RoomCharge',
      description: 'Ubernachtung 2026-08-09 - 2026-08-12',
      quantity: 3,
      unitPrice: 150,
      vatRate: 7,
      lineNet: 420.56,
      lineVat: 29.44,
      lineGross: 450,
      serviceDate: '2026-08-09',
    },
  ],
  totalNet: 420.56,
  totalVat: 29.44,
  totalGross: 450,
};

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

describe('ReservationDetailPage — aksiyon gorunurlugu', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'reservations/:id', component: ReservationDetailPage },
          { path: 'reservations', component: ReservationDetailPage },
        ]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  async function render(
    status: ReservationStatus,
    permissions: readonly PermissionKey[] = [
      PERMISSIONS.ReservationsView,
      PERMISSIONS.ReservationsCheckInOut,
      PERMISSIONS.ReservationsCreate,
    ],
  ): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const harness = await RouterTestingHarness.create('/reservations/res-1');
    http.expectOne((request) => request.url === `${baseUrl}/reservations/res-1`).flush(
      reservation(status),
    );
    http.expectOne((request) => request.url === `${baseUrl}/reservations/res-1/folio`).flush(FOLIO);
    await tick();
    harness.detectChanges();

    return { harness, element: harness.routeNativeElement as HTMLElement };
  }

  it('CheckedIn durumunda GECERSIZ gecislerin dugmesini HIC render etmez', async () => {
    // Sozlesme: `CheckedIn` -> yalnizca `CheckedOut`. Iptal ve no-show 409 verirdi.
    const { element } = await render('CheckedIn');

    expect(element.querySelector('[data-testid="reservation-check-out"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="reservation-cancel"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-no-show"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-check-in"]')).toBeNull();
  });

  it('Confirmed durumunda check-in, no-show ve iptali sunar', async () => {
    const { element } = await render('Confirmed');

    expect(element.querySelector('[data-testid="reservation-check-in"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="reservation-no-show"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="reservation-cancel"]')).not.toBeNull();
    // Check-in yapilmadan check-out olmaz.
    expect(element.querySelector('[data-testid="reservation-check-out"]')).toBeNull();
  });

  it('nihai durumda (CheckedOut) hicbir aksiyon gostermez', async () => {
    const { element } = await render('CheckedOut');

    expect(element.querySelector('[data-testid="reservation-check-in"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-check-out"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-cancel"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-no-show"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-final-note"]')?.textContent).toContain(
      'reservations.actions.finalState',
    );
  });

  it('Reservations.CheckInOut izni yoksa check-in/check-out dugmesini gostermez', async () => {
    // Iptal `Reservations.Create` ile hala gorunur — izinler ayri kapilardir.
    const { element } = await render('Confirmed', [
      PERMISSIONS.ReservationsView,
      PERMISSIONS.ReservationsCreate,
    ]);

    expect(element.querySelector('[data-testid="reservation-check-in"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-no-show"]')).toBeNull();
    expect(element.querySelector('[data-testid="reservation-cancel"]')).not.toBeNull();
  });

  it('check-out oncesi ve sonrasi odanin Dirty olacagini kullaniciya soyler', async () => {
    const { harness, element } = await render('CheckedIn');

    expect(
      element.querySelector('[data-testid="reservation-checkout-note"]')?.textContent,
    ).toContain('reservations.actions.checkOutDirtyNote');

    element.querySelector<HTMLButtonElement>('[data-testid="reservation-check-out"] button')!.click();
    await tick();

    http
      .expectOne((request) => request.url === `${baseUrl}/reservations/res-1/check-out`)
      .flush({ ...reservation('CheckedOut'), checkedOutAt: '2026-08-12T09:00:00Z' });
    await tick();
    http.expectOne((request) => request.url === `${baseUrl}/reservations/res-1/folio`).flush(FOLIO);
    await tick();
    harness.detectChanges();

    expect(
      element.querySelector('[data-testid="reservation-checkout-dirty"]')?.textContent,
    ).toContain('reservations.actions.checkOutDirtyDone');
  });

  it('409 gecersiz gecisinde sunucunun gecis metnini gosterir', async () => {
    const { harness, element } = await render('Confirmed');

    element.querySelector<HTMLButtonElement>('[data-testid="reservation-check-in"] button')!.click();
    await tick();

    http
      .expectOne((request) => request.url === `${baseUrl}/reservations/res-1/check-in`)
      .flush(
        {
          status: 409,
          title: 'Islem mevcut durumla celisiyor.',
          detail: 'Check-in giris tarihinden once yapilamaz. Giris tarihi: 2026-08-09, bugun: 2026-07-30.',
        },
        { status: 409, statusText: 'Conflict' },
      );
    await tick();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="reservation-action-error"]')?.textContent).toContain(
      'reservations.actions.conflict',
    );
    expect(element.querySelector('[data-testid="reservation-action-detail"]')?.textContent).toContain(
      'Giris tarihi: 2026-08-09',
    );
  });

  it('folio KDV kirilimini ve toplamini gosterir', async () => {
    const { element } = await render('CheckedIn');

    expect(element.querySelectorAll('[data-testid="folio-line"]')).toHaveLength(1);
    const totals = element.querySelector('[data-testid="folio-totals"]')?.textContent ?? '';
    expect(totals).toContain('420,56');
    expect(totals).toContain('29,44');
    expect(element.querySelector('[data-testid="folio-gross"]')?.textContent).toContain('450,00');
  });
});

describe('Rezervasyon durum makinesi — istemci ile sunucu birebir ayni', () => {
  it('sozlesmedeki gecis tablosunu uygular', () => {
    expect(canCheckIn('Option')).toBe(true);
    expect(canCheckIn('Confirmed')).toBe(true);
    expect(canCheckIn('CheckedIn')).toBe(false);

    expect(canCheckOut('CheckedIn')).toBe(true);
    expect(canCheckOut('Confirmed')).toBe(false);

    // `CheckedIn`/`CheckedOut` iptal edilemez.
    expect(canCancel('Option')).toBe(true);
    expect(canCancel('Confirmed')).toBe(true);
    expect(canCancel('CheckedIn')).toBe(false);
    expect(canCancel('CheckedOut')).toBe(false);

    expect(canMarkNoShow('Confirmed')).toBe(true);
    expect(canMarkNoShow('CheckedIn')).toBe(false);

    // Nihai durumlar.
    for (const status of ['CheckedOut', 'Cancelled', 'NoShow'] as const) {
      expect(canCheckIn(status)).toBe(false);
      expect(canCheckOut(status)).toBe(false);
      expect(canCancel(status)).toBe(false);
      expect(canMarkNoShow(status)).toBe(false);
    }
  });
});
