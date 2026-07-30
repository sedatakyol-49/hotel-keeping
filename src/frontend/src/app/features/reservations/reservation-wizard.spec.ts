import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AvailabilityResponse } from '../../core/models/availability.model';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import type { GuestResponse } from '../../core/models/guest.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import type { ReservationResponse } from '../../core/models/reservation.model';
import type { RoomTypeResponse } from '../../core/models/room-type.model';
import { AuthStore } from '../../core/state/auth.store';
import { ReservationWizardPage } from './reservation-wizard';

const ROOM_TYPES: readonly RoomTypeResponse[] = [
  {
    id: 't-1',
    code: 'DBL',
    name: 'Doppelzimmer',
    basePrice: 129,
    currency: 'EUR',
    capacity: 2,
    amenities: [],
    roomCount: 4,
  },
];

const GUEST: GuestResponse = {
  id: 'g-1',
  firstName: 'Jürgen',
  lastName: 'Müller',
  fullName: 'Jürgen Müller',
  email: 'juergen.mueller@example.de',
  stayCount: null,
};

const GUEST_PAGE: PagedResult<GuestResponse> = {
  items: [GUEST],
  page: 1,
  pageSize: 20,
  totalCount: 1,
};

const AVAILABILITY: AvailabilityResponse = {
  from: '2026-08-10',
  to: '2026-08-12',
  nights: 2,
  roomTypeId: null,
  totalRoomCount: 6,
  outOfOrderRoomCount: 0,
  availableRoomCount: 2,
  byRoomType: [{ roomTypeId: 't-1', roomTypeCode: 'DBL', availableRoomCount: 2 }],
  rooms: [
    { roomId: 'r-1', roomNumber: '201', floor: 2, roomTypeId: 't-1', roomTypeCode: 'DBL', capacity: 2 },
    { roomId: 'r-2', roomNumber: '202', floor: 2, roomTypeId: 't-1', roomTypeCode: 'DBL', capacity: 2 },
  ],
};

const CREATED: ReservationResponse = {
  id: 'res-1',
  reservationNumber: 'RES-2026-00042',
  status: 'Confirmed',
  channel: 'Direct',
  roomId: 'r-1',
  roomNumber: '201',
  roomTypeId: 't-1',
  roomTypeCode: 'DBL',
  guestId: 'g-1',
  guestName: 'Jürgen Müller',
  checkIn: '2026-08-10',
  checkOut: '2026-08-12',
  nights: 2,
  adults: 2,
  children: 0,
  totalAmount: 300,
  currency: 'EUR',
  depositPercent: 20,
  depositAmount: 60,
  ratePlanId: 'rp-1',
  ratePlanName: 'Sommer BAR Doppelzimmer',
  notes: null,
};

function user(): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'klaus.meier@hotel.de',
    roles: ['Manager'],
    permissions: [PERMISSIONS.ReservationsView, PERMISSIONS.ReservationsCreate],
    hotels: [{ id: 'h-1', name: 'Hotel Adler', currency: 'EUR' }],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
  };
}

/** Zoneless: `whenStable()` bekleyen promise'leri beklemez. */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('ReservationWizardPage', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'reservations/new', component: ReservationWizardPage },
          { path: 'reservations/:id', component: ReservationWizardPage },
        ]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  async function render(
    url = '/reservations/new',
  ): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    TestBed.inject(AuthStore).setSession(user());

    const harness = await RouterTestingHarness.create(url);
    http.expectOne((request) => request.url === `${baseUrl}/room-types`).flush(ROOM_TYPES);
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

  function submitForm(element: HTMLElement, testId: string): void {
    element.querySelector<HTMLFormElement>(`[data-testid="${testId}"]`)!.dispatchEvent(
      new Event('submit'),
    );
  }

  function click(element: HTMLElement, selector: string): void {
    element.querySelector<HTMLElement>(selector)!.click();
  }

  /** Adim 1 -> 4: tarih, oda ve misafir secimini tamamlar. */
  async function walkToDetails(
    harness: RouterTestingHarness,
    element: HTMLElement,
  ): Promise<void> {
    setValue(element, '#wizard-from', '2026-08-10');
    setValue(element, '#wizard-to', '2026-08-12');
    submitForm(element, 'wizard-dates-form');
    await tick();

    http.expectOne((request) => request.url === `${baseUrl}/availability`).flush(AVAILABILITY);
    await tick();
    harness.detectChanges();

    click(element, '[data-testid="wizard-room"][data-room="201"]');
    harness.detectChanges();
    click(element, '[data-testid="wizard-room-next"] button');
    harness.detectChanges();

    click(element, '[data-testid="wizard-guest"][data-guest="g-1"]');
    harness.detectChanges();
    click(element, '[data-testid="wizard-guest-next"] button');
    harness.detectChanges();
  }

  it('musaitligi yari acik aralikla sorar ve fiyat alani beklemez', async () => {
    const { element } = await render();

    setValue(element, '#wizard-from', '2026-08-10');
    setValue(element, '#wizard-to', '2026-08-12');
    submitForm(element, 'wizard-dates-form');
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/availability`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('from')).toBe('2026-08-10');
    expect(request.request.params.get('to')).toBe('2026-08-12');
    request.flush(AVAILABILITY);
    await tick();
  });

  it('POST govdesinde totalAmount GONDERMEZ ve sunucunun dondurdugu tutari gosterir', async () => {
    const { harness, element } = await render();
    await walkToDetails(harness, element);

    setValue(element, '#wizard-adults', '2');
    setValue(element, '#wizard-deposit', '20');
    submitForm(element, 'wizard-details-form');
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/reservations`);
    expect(request.request.method).toBe('POST');

    // Sozlesme: tutar her zaman sunucuda hesaplanir; istemci gondermez.
    const body = request.request.body as Record<string, unknown>;
    expect(body).not.toHaveProperty('totalAmount');
    expect(body).not.toHaveProperty('depositAmount');
    expect(body).not.toHaveProperty('nights');
    expect(body).toEqual({
      roomId: 'r-1',
      guestId: 'g-1',
      checkIn: '2026-08-10',
      checkOut: '2026-08-12',
      adults: 2,
      children: 0,
      channel: 'Direct',
      depositPercent: 20,
      notes: null,
      status: 'Confirmed',
    });
    // Sayisal alanlar gercekten sayi olarak gider (metin degil).
    expect(typeof body['adults']).toBe('number');
    expect(typeof body['children']).toBe('number');
    expect(typeof body['depositPercent']).toBe('number');

    request.flush(CREATED, { status: 201, statusText: 'Created' });
    await tick();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="wizard-done-number"]')?.textContent).toContain(
      'RES-2026-00042',
    );
    expect(element.querySelector('[data-testid="wizard-done-total"]')?.textContent).toContain('300');
    expect(element.querySelector('[data-testid="wizard-done-rate-plan"]')?.textContent).toContain(
      'Sommer BAR Doppelzimmer',
    );
  });

  it('409 cakismasinda sunucunun detail metnini gosterir ve oda adimina doner', async () => {
    const { harness, element } = await render();
    await walkToDetails(harness, element);

    submitForm(element, 'wizard-details-form');
    await tick();

    http.expectOne((candidate) => candidate.url === `${baseUrl}/reservations`).flush(
      {
        status: 409,
        title: 'Islem mevcut durumla celisiyor.',
        detail:
          "'201' numarali oda 2026-08-10 - 2026-08-12 araliginda musait degil: 'RES-2026-00001' rezervasyonu (2026-08-09 - 2026-08-11) ile cakisiyor.",
      },
      { status: 409, statusText: 'Conflict' },
    );
    await tick();

    // Cakismadan sonra musaitlik yeniden sorulur (liste bayatlamis olabilir).
    http.expectOne((candidate) => candidate.url === `${baseUrl}/availability`).flush(AVAILABILITY);
    await tick();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="wizard-error"]')?.textContent).toContain(
      'reservations.wizard.conflict',
    );
    expect(
      element.querySelector('[data-testid="wizard-conflict-detail"]')?.textContent,
    ).toContain('RES-2026-00001');
    // Kullanici oda adimindadir ve baska bir oda secebilir.
    expect(element.querySelector('[data-testid="wizard-room"]')).not.toBeNull();
  });

  it('gecersiz tarih araliginda (cikis <= giris) istek gondermez', async () => {
    const { element, harness } = await render();

    setValue(element, '#wizard-from', '2026-08-10');
    setValue(element, '#wizard-to', '2026-08-10');
    harness.detectChanges();

    submitForm(element, 'wizard-dates-form');
    await tick();

    http.expectNone((request) => request.url === `${baseUrl}/availability`);
    expect(element.querySelector('[data-testid="wizard-stay-error"]')?.textContent).toContain(
      'reservations.wizard.validation.stayTooShort',
    );
  });

  it('kapasiteyi asan kisi sayisinda kaydi engeller (sunucuya bos istek gitmez)', async () => {
    const { harness, element } = await render();
    await walkToDetails(harness, element);

    // Oda kapasitesi 2; 3 yetiskin + 1 cocuk sunucuda 400 uretirdi.
    setValue(element, '#wizard-adults', '3');
    setValue(element, '#wizard-children', '1');
    harness.detectChanges();

    expect(element.querySelector('[data-testid="wizard-capacity-error"]')?.textContent).toContain(
      'reservations.wizard.validation.capacity',
    );

    submitForm(element, 'wizard-details-form');
    await tick();
    http.expectNone((request) => request.url === `${baseUrl}/reservations`);
  });

  it('doluluk izgarasindan gelen oda/tarih on-doldurmasini kullanir', async () => {
    const { harness, element } = await render(
      '/reservations/new?roomId=r-2&from=2026-08-10&to=2026-08-12',
    );

    expect(element.querySelector<HTMLInputElement>('#wizard-from')?.value).toBe('2026-08-10');

    submitForm(element, 'wizard-dates-form');
    await tick();
    http.expectOne((request) => request.url === `${baseUrl}/availability`).flush(AVAILABILITY);
    await tick();
    harness.detectChanges();

    // On-doldurulmus oda hala musaitse dogrudan secili gelir.
    click(element, '[data-testid="wizard-room-next"] button');
    harness.detectChanges();
    expect(element.querySelector('[data-testid="wizard-guest"]')).not.toBeNull();
  });
});
