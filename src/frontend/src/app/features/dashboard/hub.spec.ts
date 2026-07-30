import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import type { HousekeepingBoardResponse, RoomResponse } from '../../core/models/room.model';
import type { RoomTypeResponse } from '../../core/models/room-type.model';
import { AuthStore } from '../../core/state/auth.store';
import { HubPage } from './hub';
import { HubStore } from './hub.store';

/** `GET /rooms` — hub yalnizca `totalCount` kullanir. */
const ROOM_PAGE: PagedResult<RoomResponse> = {
  items: [],
  page: 1,
  pageSize: 1,
  totalCount: 13,
};

/** `GET /rooms/board` — seed verisiyle ayni dagilim. */
const BOARD: HousekeepingBoardResponse = {
  floors: [],
  summary: { clean: 9, dirty: 1, inspected: 1, outOfOrder: 2, total: 13 },
};

function roomType(code: string): RoomTypeResponse {
  return {
    id: `t-${code}`,
    code,
    name: code,
    basePrice: 129,
    currency: 'EUR',
    capacity: 2,
    amenities: ['wifi'],
    roomCount: 4,
  };
}

const ROOM_TYPES: readonly RoomTypeResponse[] = [
  roomType('SGL'),
  roomType('DBL'),
  roomType('SUI'),
];

function user(permissions: readonly PermissionKey[]): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'klaus.meier@hotel.de',
    roles: ['Receptionist'],
    permissions,
    hotels: [{ id: 'h-1', name: 'Hotel Adler', currency: 'EUR' }],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
  };
}

describe('HubPage — modul kart izgarasi', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => {
    http.verify();
  });

  /**
   * Bileseni olusturur ve ozet yuklemesini baslatan effect'i tetikler.
   * Istekler `HttpTestingController` uzerinde **acik** kalir; her test kendi
   * yanitini verir (bu yuzden burada `whenStable` beklenmez).
   */
  function render(permissions: readonly PermissionKey[]): ComponentFixture<HubPage> {
    TestBed.inject(AuthStore).setSession(user(permissions));
    const fixture = TestBed.createComponent(HubPage);
    fixture.detectChanges();
    TestBed.tick();
    return fixture;
  }

  function cardPaths(fixture: ComponentFixture<HubPage>): string[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>(
        '[data-testid="hub-card"]',
      ),
    ).map((card) => card.dataset['path'] ?? '');
  }

  function sectionKeys(fixture: ComponentFixture<HubPage>): string[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>(
        '[data-testid="hub-section"]',
      ),
    ).map((section) => section.dataset['section'] ?? '');
  }

  it('Rooms.View olan kullaniciya yalnizca izinli kartlari gosterir', async () => {
    const fixture = render([PERMISSIONS.RoomsView]);
    http.expectOne((request) => request.url === `${baseUrl}/rooms`).flush(ROOM_PAGE);
    await fixture.whenStable();
    fixture.detectChanges();

    // `/rooms/types` -> Rooms.Manage, `/housekeeping` -> Housekeeping.View gerektirir.
    expect(cardPaths(fixture)).toEqual(['/rooms']);
    // Hub'in kendisi (`/dashboard`) kart olarak tekrar etmez -> overview bolumu yok.
    expect(sectionKeys(fixture)).toEqual(['nav.section.operations']);
  });

  it('Rooms.View olmayan kullaniciya oda kartini hic render etmez', async () => {
    const fixture = render([PERMISSIONS.InvoicesView]);

    expect(cardPaths(fixture)).toEqual(['/invoices']);
    expect(sectionKeys(fixture)).toEqual(['nav.section.finance']);
  });

  it('tum kartlari suzulen bolumun basligini da gizler', async () => {
    // Housekeeping rolu: finans bolumunun tek karti Faturalar'dir ve izin yoktur.
    const fixture = render([PERMISSIONS.HousekeepingView, PERMISSIONS.HousekeepingUpdate]);
    http.expectOne((request) => request.url === `${baseUrl}/rooms/board`).flush(BOARD);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(cardPaths(fixture)).toEqual(['/housekeeping']);
    expect(sectionKeys(fixture)).not.toContain('nav.section.finance');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('nav.invoices');
  });

  it('hazir olmayan modulleri isaretler, calisan modulu isaretlemez', async () => {
    const fixture = render([PERMISSIONS.RoomsView, PERMISSIONS.ReservationsView]);
    http.expectOne((request) => request.url === `${baseUrl}/rooms`).flush(ROOM_PAGE);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const rooms = element.querySelector<HTMLElement>('[data-path="/rooms"]');
    const reservations = element.querySelector<HTMLElement>('[data-path="/reservations"]');

    expect(rooms?.dataset['planned']).toBe('false');
    expect(reservations?.dataset['planned']).toBe('true');
    // Hazir olmayan modul de tiklanabilir kalir (iskelet sayfaya gider).
    expect(reservations?.querySelector('a')?.getAttribute('href')).toBe('/reservations');
    expect(reservations?.textContent).toContain('hub.status.planned');
  });

  it('kart baglantisi aciklama ve ozet satirini `aria-describedby` ile duyurur', async () => {
    const fixture = render([PERMISSIONS.RoomsView]);
    http.expectOne((request) => request.url === `${baseUrl}/rooms`).flush(ROOM_PAGE);
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const link = element.querySelector<HTMLAnchorElement>('[data-path="/rooms"] a');
    const describedBy = link?.getAttribute('aria-describedby')?.split(' ') ?? [];

    expect(describedBy).toHaveLength(2);
    for (const id of describedBy) {
      expect(element.querySelector(`#${id}`)).not.toBeNull();
    }
    // Kart icinde tek bir interaktif eleman vardir (ic ice odak tuzagi yok).
    expect(
      element.querySelectorAll('[data-path="/rooms"] a, [data-path="/rooms"] button'),
    ).toHaveLength(1);
  });

  it('oda kartini `rooms` + `board` yanitlarindan tek istek setiyle turetir', async () => {
    const fixture = render([
      PERMISSIONS.RoomsView,
      PERMISSIONS.RoomsManage,
      PERMISSIONS.HousekeepingView,
    ]);

    http.expectOne((request) => request.url === `${baseUrl}/rooms`).flush(ROOM_PAGE);
    http.expectOne((request) => request.url === `${baseUrl}/room-types`).flush(ROOM_TYPES);
    // Board yalnizca **bir kez** cagrilir; Odalar ve Housekeeping kartlari ayni yaniti okur.
    http.expectOne((request) => request.url === `${baseUrl}/rooms/board`).flush(BOARD);
    await fixture.whenStable();
    fixture.detectChanges();

    const summaries = TestBed.inject(HubStore).summaries();
    expect(summaries.rooms).toEqual({
      state: 'ready',
      textKey: 'hub.summary.roomsDirty',
      params: { count: 13, dirty: 1 },
    });
    expect(summaries.roomTypes).toEqual({
      state: 'ready',
      textKey: 'hub.summary.roomTypes',
      params: { count: 3 },
    });
    // Acik is = dirty (1) + clean (9); Inspected ve OutOfOrder sayilmaz.
    expect(summaries.housekeeping).toEqual({
      state: 'ready',
      textKey: 'hub.summary.housekeeping',
      params: { count: 10, dirty: 1 },
    });
  });

  it('Housekeeping.View yoksa board ucunu hic cagirmaz', async () => {
    const fixture = render([PERMISSIONS.RoomsView, PERMISSIONS.RoomsManage]);

    http.expectOne((request) => request.url === `${baseUrl}/rooms`).flush(ROOM_PAGE);
    http.expectOne((request) => request.url === `${baseUrl}/room-types`).flush(ROOM_TYPES);
    // 403 uretmemek icin izinsiz uc hic istenmez.
    http.expectNone((request) => request.url === `${baseUrl}/rooms/board`);
    await fixture.whenStable();
    fixture.detectChanges();

    const summaries = TestBed.inject(HubStore).summaries();
    // Kirli oda bilgisi olmadan yalnizca toplam gosterilir.
    expect(summaries.rooms).toEqual({
      state: 'ready',
      textKey: 'hub.summary.rooms',
      params: { count: 13 },
    });
    expect(summaries.housekeeping).toBeNull();
  });

  it('gorunur kart istemedigi ucu cagirmaz (Rooms.Manage yoksa room-types yok)', async () => {
    const fixture = render([PERMISSIONS.RoomsView]);

    http.expectOne((request) => request.url === `${baseUrl}/rooms`).flush(ROOM_PAGE);
    http.expectNone((request) => request.url === `${baseUrl}/room-types`);
    await fixture.whenStable();

    expect(TestBed.inject(HubStore).summaries().roomTypes).toBeNull();
  });

  it('veri hatasinda kart tiklanabilir kalir ve sayi yerine hata gosterir', async () => {
    const fixture = render([PERMISSIONS.RoomsView]);
    http
      .expectOne((request) => request.url === `${baseUrl}/rooms`)
      .flush({ title: 'Server error', status: 500 }, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(TestBed.inject(HubStore).summaries().rooms).toEqual({
      state: 'error',
      textKey: 'hub.summary.unavailable',
      params: {},
    });
    expect(element.querySelector('[data-path="/rooms"] a')?.getAttribute('href')).toBe('/rooms');
    expect(element.querySelector('[data-path="/rooms"] [data-testid="hub-summary"]')?.textContent).toContain(
      'hub.summary.unavailable',
    );
  });
});
