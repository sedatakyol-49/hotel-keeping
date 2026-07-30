import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { convertToParamMap } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { PagedResult } from '../../core/models/paged-result.model';
import type { RoomResponse } from '../../core/models/room.model';
import {
  DEFAULT_ROOM_LIST_QUERY,
  hasActiveRoomFilters,
  parseRoomListQuery,
  roomListQueryToParams,
  withFilterChange,
} from './room-list-query';
import { RoomsStore } from './rooms.store';

function room(number: string, floor: number): RoomResponse {
  return {
    id: `room-${number}`,
    number,
    floor,
    roomTypeId: 'type-1',
    roomTypeCode: 'DBL',
    roomTypeName: 'Doppelzimmer',
    housekeepingStatus: 'Dirty',
    isOutOfOrder: false,
    note: null,
  };
}

function page(items: readonly RoomResponse[], overrides: Partial<PagedResult<RoomResponse>> = {}) {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    ...overrides,
  } satisfies PagedResult<RoomResponse>;
}

describe('room listesi sorgu cozumleme', () => {
  it('gecersiz degerleri varsayilana dusurur', () => {
    const query = parseRoomListQuery(
      convertToParamMap({
        page: '0',
        pageSize: '7',
        floor: '400',
        housekeepingStatus: 'Sparkling',
        search: '   ',
      }),
    );

    expect(query).toEqual({
      page: DEFAULT_ROOM_LIST_QUERY.page,
      pageSize: DEFAULT_ROOM_LIST_QUERY.pageSize,
      roomTypeId: null,
      floor: null,
      housekeepingStatus: null,
      search: null,
    });
    expect(hasActiveRoomFilters(query)).toBe(false);
  });

  it('gecerli filtreleri okur ve URL parametrelerine geri cevirir', () => {
    const query = parseRoomListQuery(
      convertToParamMap({
        page: '3',
        pageSize: '50',
        floor: '-1',
        roomTypeId: 'type-1',
        housekeepingStatus: 'Inspected',
        search: ' 20 ',
      }),
    );

    expect(query).toEqual({
      page: 3,
      pageSize: 50,
      roomTypeId: 'type-1',
      floor: -1,
      housekeepingStatus: 'Inspected',
      search: '20',
    });
    expect(hasActiveRoomFilters(query)).toBe(true);
    expect(roomListQueryToParams(query)).toEqual({
      page: 3,
      pageSize: 50,
      roomTypeId: 'type-1',
      floor: -1,
      housekeepingStatus: 'Inspected',
      search: '20',
    });
  });

  it('varsayilan degerleri URL e yazmaz ve filtre degisiminde ilk sayfaya doner', () => {
    const query = withFilterChange(
      { page: 5, pageSize: 20, search: null, floor: null },
      { housekeepingStatus: 'Clean' },
    );

    expect(query.page).toBe(1);
    expect(roomListQueryToParams(query)).toEqual({ housekeepingStatus: 'Clean' });
  });
});

describe('RoomsStore', () => {
  let store: RoomsStore;
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(RoomsStore);
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  it('yalnizca dolu filtreleri sorgu dizesine ekler', async () => {
    const loading = store.load({
      page: 2,
      pageSize: 50,
      roomTypeId: 'type-1',
      floor: 0,
      housekeepingStatus: 'Dirty',
      search: '  20  ',
    });

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/rooms`);
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('roomTypeId')).toBe('type-1');
    // `floor = 0` gecerli bir degerdir, atlanmamalidir.
    expect(request.request.params.get('floor')).toBe('0');
    expect(request.request.params.get('housekeepingStatus')).toBe('Dirty');
    expect(request.request.params.get('search')).toBe('20');

    request.flush(page([room('201', 2)], { page: 2, pageSize: 50, totalCount: 51 }));
    await loading;

    expect(store.loading()).toBe(false);
    expect(store.items()).toHaveLength(1);
  });

  it('bos filtreleri sorgu dizesinden cikarir', async () => {
    const loading = store.load({
      page: 1,
      pageSize: 20,
      roomTypeId: null,
      floor: null,
      housekeepingStatus: null,
      search: '',
    });

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/rooms`);
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);

    request.flush(page([]));
    await loading;

    expect(store.isEmpty()).toBe(true);
    expect(store.hasFilters()).toBe(false);
  });

  it('sayfalama durumunu sunucu yanitindan hesaplar', async () => {
    const loading = store.load({ page: 2, pageSize: 20 });
    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/rooms`)
      .flush(page([room('301', 3), room('302', 3)], { page: 2, pageSize: 20, totalCount: 45 }));
    await loading;

    expect(store.page()).toBe(2);
    expect(store.totalPages()).toBe(3);
    expect(store.hasPreviousPage()).toBe(true);
    expect(store.hasNextPage()).toBe(true);
    expect(store.rangeStart()).toBe(21);
    expect(store.rangeEnd()).toBe(22);
  });

  it('gecikmis yaniti yok sayar; ekranda son sorgunun sonucu kalir', async () => {
    const first = store.load({ page: 1, pageSize: 20, search: 'eski' });
    const firstRequest = http.expectOne((candidate) => candidate.params.get('search') === 'eski');

    const second = store.load({ page: 1, pageSize: 20, search: 'yeni' });
    const secondRequest = http.expectOne((candidate) => candidate.params.get('search') === 'yeni');

    // Yeni sorgu once, eski sorgu sonra cevaplanir.
    secondRequest.flush(page([room('501', 5)]));
    firstRequest.flush(page([room('101', 1), room('102', 1)]));
    await Promise.all([first, second]);

    expect(store.items().map((item) => item.number)).toEqual(['501']);
    expect(store.query().search).toBe('yeni');
    expect(store.loading()).toBe(false);
  });

  it('hata durumunda listeyi bosaltir ve hatayi tasir', async () => {
    const loading = store.load(DEFAULT_ROOM_LIST_QUERY);
    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/rooms`)
      .flush({ title: 'Server error', status: 500 }, { status: 500, statusText: 'Server Error' });
    await loading;

    expect(store.error()?.status).toBe(500);
    expect(store.items()).toEqual([]);
    // Hata varken "bos durum" gosterilmez; hata blogu gosterilir.
    expect(store.isEmpty()).toBe(false);
  });

  it('409 silme cakismasini alan hatasi olarak saklar', async () => {
    const loading = store.load(DEFAULT_ROOM_LIST_QUERY);
    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/rooms`)
      .flush(page([room('201', 2)]));
    await loading;

    const removing = store.remove('room-201');
    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/rooms/room-201`)
      .flush(
        { title: 'Conflict', status: 409, detail: 'Future reservations exist' },
        { status: 409, statusText: 'Conflict' },
      );
    const error = await removing;

    expect(error?.status).toBe(409);
    expect(store.deleteError()?.status).toBe(409);
    expect(store.deletingId()).toBeNull();
  });

  it('silme sonrasi son sayfa bosalirsa bir onceki sayfayi yukler', async () => {
    const loading = store.load({ page: 3, pageSize: 20 });
    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/rooms`)
      .flush(page([room('401', 4)], { page: 3, pageSize: 20, totalCount: 41 }));
    await loading;

    const removing = store.remove('room-401');
    http.expectOne((candidate) => candidate.url === `${baseUrl}/rooms/room-401`).flush(null);

    // Yeniden yukleme, silme yaniti cozuldukten sonra (mikro-gorevde) tetiklenir.
    await new Promise((resolve) => setTimeout(resolve, 0));

    const reload = http.expectOne((candidate) => candidate.url === `${baseUrl}/rooms`);
    expect(reload.request.params.get('page')).toBe('2');
    reload.flush(page([room('301', 3)], { page: 2, pageSize: 20, totalCount: 40 }));

    expect(await removing).toBeNull();
    expect(store.page()).toBe(2);
  });

  afterEach(() => {
    http.verify();
  });
});
