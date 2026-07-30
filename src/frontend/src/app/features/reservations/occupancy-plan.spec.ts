import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { OccupancyResponse } from '../../core/models/availability.model';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';
import { OccupancyPlanPage } from './occupancy-plan';
import {
  DEFAULT_OCCUPANCY_NIGHTS,
  clampOccupancyRange,
  occupancyRangeToParams,
  parseOccupancyRange,
  resizeOccupancyRange,
  shiftOccupancyRange,
} from './occupancy-query';

const FROM = '2026-08-09';
const TO = '2026-08-16';
const DAYS = [
  '2026-08-09',
  '2026-08-10',
  '2026-08-11',
  '2026-08-12',
  '2026-08-13',
  '2026-08-14',
  '2026-08-15',
];

const RESPONSE: OccupancyResponse = {
  from: FROM,
  to: TO,
  days: DAYS,
  rooms: [
    {
      roomId: 'r-1',
      roomNumber: '201',
      floor: 2,
      roomTypeId: 't-1',
      roomTypeCode: 'DBL',
      isOutOfOrder: false,
      cells: [
        {
          date: '2026-08-10',
          reservationId: 'res-1',
          reservationNumber: 'RES-2026-00001',
          guestName: 'Jürgen Müller',
          status: 'Confirmed',
          isArrival: true,
          isDeparture: false,
        },
        {
          date: '2026-08-11',
          reservationId: 'res-1',
          reservationNumber: 'RES-2026-00001',
          guestName: 'Jürgen Müller',
          status: 'Confirmed',
          isArrival: false,
          isDeparture: true,
        },
      ],
    },
  ],
  summary: { roomCount: 1, days: 7, roomNights: 7, occupiedRoomNights: 2, occupancyRate: 28.57 },
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

describe('OccupancyPlanPage — oda × gun izgarasi', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'reservations/occupancy', component: OccupancyPlanPage },
          { path: 'reservations/new', component: OccupancyPlanPage },
          { path: 'reservations/:id', component: OccupancyPlanPage },
        ]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  async function render(
    permissions: readonly PermissionKey[] = [PERMISSIONS.ReservationsView],
    url = `/reservations/occupancy?from=${FROM}&to=${TO}`,
    response: OccupancyResponse = RESPONSE,
  ): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const harness = await RouterTestingHarness.create(url);
    http.expectOne((request) => request.url === `${baseUrl}/occupancy`).flush(response);
    await tick();
    harness.detectChanges();

    return { harness, element: harness.routeNativeElement as HTMLElement };
  }

  it('seyrek hucrelerden TEK bir kesintisiz cubuk kurar (colspan = gece sayisi)', async () => {
    const { element } = await render();

    const bars = element.querySelectorAll('[data-testid="occupancy-bar-cell"]');
    expect(bars).toHaveLength(1);
    // Iki gece tek hucrede birlesir: araya cetvel cizgisi girmez.
    expect(bars[0].getAttribute('colspan')).toBe('2');
    expect(bars[0].getAttribute('data-reservation')).toBe('RES-2026-00001');

    // Cikis gunu icin hucre uretilmedigi icin o kolon bos gecedir.
    const freeDates = [...element.querySelectorAll('[data-testid="occupancy-free-cell"]')].map(
      (cell) => cell.getAttribute('data-date'),
    );
    expect(freeDates).toContain('2026-08-12');
    expect(freeDates).not.toContain('2026-08-10');

    // Cubuk her iki ucta da kapalidir (konaklama pencerede basliyor ve bitiyor).
    const bar = element.querySelector('[data-testid="occupancy-bar"]');
    expect(bar?.getAttribute('data-starts')).toBe('true');
    expect(bar?.getAttribute('data-ends')).toBe('true');
  });

  it('baslik ve satirlari ayni tabloda, birebir esit sutunlarla hizalar', async () => {
    const { element } = await render();

    const table = element.querySelector<HTMLTableElement>('table');
    expect(table?.classList.contains('table-fixed')).toBe(true);
    // `colgroup`: etiket sutunu + gun sayisi.
    expect(table!.querySelectorAll('colgroup col')).toHaveLength(DAYS.length + 1);
    expect(element.querySelectorAll('[data-testid="occupancy-day-head"]')).toHaveLength(
      DAYS.length,
    );

    // Satirdaki hucrelerin colspan toplami baslik hucre sayisina esit olmali.
    const row = table!.querySelector('tbody tr')!;
    const spans = [...row.children].reduce(
      (sum, cell) => sum + Number(cell.getAttribute('colspan') ?? 1),
      0,
    );
    expect(spans).toBe(table!.querySelectorAll('thead th').length);

    // Sticky etiket sutunu + sticky baslik satiri.
    expect(table!.querySelector('thead th')?.className).toContain('sticky');
    expect(table!.querySelector('tbody th')?.className).toContain('sticky');
  });

  it('92 gunu asan araligi ISTEMCIDE kirpar ve sunucudan gecersiz aralik istemez', async () => {
    // Kullanici elle bir yillik aralik yazsa bile istek 92 gunle sinirlidir.
    TestBed.inject(AuthStore).setSession(user([PERMISSIONS.ReservationsView]));
    const harness = await RouterTestingHarness.create(
      '/reservations/occupancy?from=2026-01-01&to=2026-12-31',
    );

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/occupancy`);
    expect(request.request.params.get('from')).toBe('2026-01-01');
    expect(request.request.params.get('to')).toBe('2026-04-03');

    request.flush({ ...RESPONSE, from: '2026-01-01', to: '2026-04-03' });
    await tick();
    harness.detectChanges();

    // Kirpma sessizce yapilmaz; kullaniciya aciklanir.
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="occupancy-clamped"]')?.textContent).toContain(
      'occupancy.range.clamped',
    );
  });

  it('Reservations.Create olmadan bos geceyi sihirbaz baglantisi yapmaz', async () => {
    const { element } = await render([PERMISSIONS.ReservationsView]);

    expect(element.querySelectorAll('[data-testid="occupancy-free-cell"]').length).toBeGreaterThan(
      0,
    );
    expect(element.querySelector('[data-testid="occupancy-free-link"]')).toBeNull();
  });

  it('Reservations.Create ile bos gece sihirbaza on-doldurulmus baglanti verir', async () => {
    const { element } = await render([PERMISSIONS.ReservationsView, PERMISSIONS.ReservationsCreate]);

    const link = element.querySelector<HTMLAnchorElement>('[data-testid="occupancy-free-link"]');
    expect(link).not.toBeNull();
    expect(link!.getAttribute('href')).toContain('/reservations/new');
    expect(link!.getAttribute('href')).toContain('from=2026-08-09');
  });
});

describe('occupancy-query — 92 gun siniri ve URL senkronu', () => {
  const now = new Date(Date.UTC(2026, 7, 9));

  it('sunucu tavanini asan araligi kirpar ve bayrak birakir', () => {
    const clamped = clampOccupancyRange('2026-01-01', '2026-12-31');
    expect(clamped.to).toBe('2026-04-03');
    expect(clamped.clamped).toBe(true);

    const within = clampOccupancyRange('2026-01-01', '2026-02-01');
    expect(within.to).toBe('2026-02-01');
    expect(within.clamped).toBe(false);
  });

  it('ters veya sifir araligi en az bir geceye cikarir', () => {
    expect(clampOccupancyRange('2026-08-10', '2026-08-10').to).toBe('2026-08-11');
    expect(clampOccupancyRange('2026-08-10', '2026-08-01').to).toBe('2026-08-11');
  });

  it('gecersiz/eksik parametrede varsayilan pencereye duser', () => {
    const range = parseOccupancyRange(convertToParamMap({ from: 'nicht-datum' }), now);
    expect(range.from).toBe('2026-08-09');
    expect(range.to).toBe('2026-08-23');
  });

  it('varsayilan pencereyi adres cubuguna yazmaz', () => {
    expect(occupancyRangeToParams({ from: '2026-08-09', to: '2026-08-23', clamped: false }, now))
      .toEqual({ from: null, to: null });
    expect(occupancyRangeToParams({ from: '2026-09-01', to: '2026-09-08', clamped: false }, now))
      .toEqual({ from: '2026-09-01', to: '2026-09-08' });
  });

  it('pencereyi kendi genisligi kadar kaydirir ve genislik degisikligini kirpar', () => {
    const range = { from: '2026-08-09', to: '2026-08-16', clamped: false };
    expect(shiftOccupancyRange(range, 1)).toMatchObject({ from: '2026-08-16', to: '2026-08-23' });
    expect(shiftOccupancyRange(range, -1)).toMatchObject({ from: '2026-08-02', to: '2026-08-09' });
    // 120 gunluk pencere istense de 92'ye kirpilir.
    expect(resizeOccupancyRange(range, 120)).toMatchObject({
      to: '2026-11-09',
      clamped: true,
    });
    expect(DEFAULT_OCCUPANCY_NIGHTS).toBe(14);
  });
});
