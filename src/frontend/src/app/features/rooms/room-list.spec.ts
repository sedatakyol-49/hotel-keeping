import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import type { RoomResponse } from '../../core/models/room.model';
import type { RoomTypeResponse } from '../../core/models/room-type.model';
import { AuthStore } from '../../core/state/auth.store';
import { RoomListPage } from './room-list';

const ROOM: RoomResponse = {
  id: 'room-1',
  number: '201',
  floor: 2,
  roomTypeId: 'type-1',
  roomTypeCode: 'DBL',
  roomTypeName: 'Doppelzimmer',
  housekeepingStatus: 'Dirty',
  isOutOfOrder: false,
  note: null,
};

const ROOM_TYPE: RoomTypeResponse = {
  id: 'type-1',
  code: 'DBL',
  name: 'Doppelzimmer',
  basePrice: 120,
  currency: 'EUR',
  capacity: 2,
  sizeSqm: 24,
  amenities: ['wifi'],
  roomCount: 12,
};

const ROOM_PAGE: PagedResult<RoomResponse> = {
  items: [ROOM],
  page: 1,
  pageSize: 20,
  totalCount: 1,
};

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

describe('RoomListPage — RBAC gorunurlugu', () => {
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

  async function render(
    permissions: readonly PermissionKey[],
  ): Promise<ComponentFixture<RoomListPage>> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const fixture = TestBed.createComponent(RoomListPage);
    fixture.detectChanges();

    http.expectOne((request) => request.url === `${baseUrl}/room-types`).flush([ROOM_TYPE]);
    http.expectOne((request) => request.url === `${baseUrl}/rooms`).flush(ROOM_PAGE);

    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('Rooms.Manage izni olmadan yazma aksiyonlarini hic render etmez', async () => {
    const fixture = await render([PERMISSIONS.RoomsView]);
    const element = fixture.nativeElement as HTMLElement;

    // Liste yine de gorunur (okuma izni var).
    expect(element.textContent).toContain('201');
    expect(element.querySelectorAll('[data-testid="room-edit"]')).toHaveLength(0);
    expect(element.querySelectorAll('[data-testid="room-delete"]')).toHaveLength(0);
    expect(element.querySelector('[data-testid="rooms-create"]')).toBeNull();
    expect(element.querySelector('[data-testid="rooms-manage-types"]')).toBeNull();
  });

  it('Rooms.Manage izniyle olusturma, duzenleme ve silme aksiyonlarini gosterir', async () => {
    const fixture = await render([PERMISSIONS.RoomsView, PERMISSIONS.RoomsManage]);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="rooms-create"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="rooms-manage-types"]')).not.toBeNull();
    // Masaustu tablo + mobil kart ayni store'u okur; her ikisinde de aksiyon vardir.
    expect(element.querySelectorAll('[data-testid="room-edit"]').length).toBeGreaterThan(0);
    expect(element.querySelectorAll('[data-testid="room-delete"]').length).toBeGreaterThan(0);
  });

  it('duzenleme baglantisi dogru rotaya isaret eder', async () => {
    const fixture = await render([PERMISSIONS.RoomsView, PERMISSIONS.RoomsManage]);
    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>(
      '[data-testid="room-edit"]',
    );

    expect(link?.getAttribute('href')).toBe('/rooms/room-1/edit');
  });
});
