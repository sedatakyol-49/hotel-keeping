import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type {
  HousekeepingBoardResponse,
  HousekeepingBoardRoom,
  RoomResponse,
} from '../../core/models/room.model';
import { HousekeepingStore, computeSummary } from './housekeeping.store';

const BOARD: HousekeepingBoardResponse = {
  floors: [
    {
      floor: 1,
      rooms: [
        {
          id: 'r-101',
          number: '101',
          roomTypeCode: 'DBL',
          housekeepingStatus: 'Dirty',
          isOutOfOrder: false,
          note: null,
        },
        {
          id: 'r-102',
          number: '102',
          roomTypeCode: 'DBL',
          housekeepingStatus: 'Clean',
          isOutOfOrder: false,
          note: null,
        },
      ],
    },
    {
      floor: 2,
      rooms: [
        {
          id: 'r-201',
          number: '201',
          roomTypeCode: 'SGL',
          housekeepingStatus: 'Clean',
          isOutOfOrder: false,
          note: 'Fenster prüfen',
        },
      ],
    },
  ],
  summary: { clean: 2, dirty: 1, inspected: 0, outOfOrder: 0, total: 3 },
};

function patchedRoom(overrides: Partial<RoomResponse> = {}): RoomResponse {
  return {
    id: 'r-101',
    number: '101',
    floor: 1,
    roomTypeId: 'type-1',
    roomTypeCode: 'DBL',
    roomTypeName: 'Doppelzimmer',
    housekeepingStatus: 'Inspected',
    isOutOfOrder: false,
    note: null,
    ...overrides,
  };
}

describe('HousekeepingStore', () => {
  let store: HousekeepingStore;
  let http: HttpTestingController;
  let baseUrl: string;

  function findRoom(id: string): HousekeepingBoardRoom | undefined {
    return store
      .floors()
      .flatMap((floor) => floor.rooms)
      .find((room) => room.id === id);
  }

  /** Test icinde oda yoksa hemen kirilsin diye ayri yardimci. */
  function requireRoom(id: string): HousekeepingBoardRoom {
    const room = findRoom(id);
    if (!room) {
      throw new Error(`Test verisinde oda bulunamadi: ${id}`);
    }
    return room;
  }

  async function loadBoard(): Promise<void> {
    const loading = store.load();
    http.expectOne(`${baseUrl}/rooms/board`).flush(BOARD);
    await loading;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(HousekeepingStore);
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => {
    http.verify();
  });

  it('panoyu katlara gore yukler ve sunucu sayaclarini kullanir', async () => {
    await loadBoard();

    expect(store.floors()).toHaveLength(2);
    expect(store.summary()).toEqual(BOARD.summary);
    expect(store.isEmpty()).toBe(false);
  });

  it('durum degisikligini iyimser uygular ve sayaclari aninda gunceller', async () => {
    await loadBoard();
    const room = requireRoom('r-101');

    const pending = store.changeStatus(room, 'Inspected');

    // Yanit gelmeden once ekranda yeni durum gorunur.
    expect(findRoom('r-101')?.housekeepingStatus).toBe('Inspected');
    expect(store.summary()).toEqual({ clean: 2, dirty: 0, inspected: 1, outOfOrder: 0, total: 3 });
    expect(store.isPending('r-101')).toBe(true);

    const request = http.expectOne(`${baseUrl}/rooms/r-101/housekeeping`);
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ status: 'Inspected', note: null });
    request.flush(patchedRoom({ note: 'Kontrolliert' }));

    expect(await pending).toBe(true);
    expect(findRoom('r-101')?.note).toBe('Kontrolliert');
    expect(store.isPending('r-101')).toBe(false);
    expect(store.updateError()).toBeNull();
    expect(store.announcement()).toEqual({
      kind: 'updated',
      roomNumber: '101',
      status: 'Inspected',
    });
  });

  it('hata durumunda iyimser degisikligi geri alir (oda + sayaclar)', async () => {
    await loadBoard();
    const room = requireRoom('r-101');

    const pending = store.changeStatus(room, 'OutOfOrder');
    expect(findRoom('r-101')?.housekeepingStatus).toBe('OutOfOrder');
    expect(findRoom('r-101')?.isOutOfOrder).toBe(true);
    expect(store.summary().outOfOrder).toBe(1);

    http
      .expectOne(`${baseUrl}/rooms/r-101/housekeeping`)
      .flush({ title: 'Conflict', status: 409 }, { status: 409, statusText: 'Conflict' });

    expect(await pending).toBe(false);
    // Geri alma: oda ve sayaclar yukleme anindaki haline doner.
    expect(findRoom('r-101')?.housekeepingStatus).toBe('Dirty');
    expect(findRoom('r-101')?.isOutOfOrder).toBe(false);
    expect(store.summary()).toEqual(BOARD.summary);
    expect(store.updateError()?.status).toBe(409);
    expect(store.announcement()).toEqual({
      kind: 'failed',
      roomNumber: '101',
      status: 'Dirty',
    });
    expect(store.isPending('r-101')).toBe(false);
  });

  it('not duzenlerken durumu korur, bos not null olarak gonderilir', async () => {
    await loadBoard();
    const room = requireRoom('r-201');

    const pending = store.changeNote(room, '   ');
    const request = http.expectOne(`${baseUrl}/rooms/r-201/housekeeping`);
    expect(request.request.body).toEqual({ status: 'Clean', note: null });

    request.flush(
      patchedRoom({ id: 'r-201', number: '201', housekeepingStatus: 'Clean', note: null }),
    );
    expect(await pending).toBe(true);
    expect(findRoom('r-201')?.note).toBeNull();
  });

  it('durum filtresi bos katlari gizler', async () => {
    await loadBoard();

    store.setStatusFilter('Dirty');
    expect(store.visibleFloors()).toHaveLength(1);
    expect(store.visibleRoomCount()).toBe(1);

    store.setStatusFilter('Inspected');
    expect(store.visibleFloors()).toHaveLength(0);
    expect(store.visibleRoomCount()).toBe(0);

    store.setStatusFilter(null);
    expect(store.visibleRoomCount()).toBe(3);
  });

  it('sayaclari kat agacindan yeniden hesaplar', () => {
    expect(computeSummary(BOARD.floors)).toEqual(BOARD.summary);
    expect(computeSummary([])).toEqual({
      clean: 0,
      dirty: 0,
      inspected: 0,
      outOfOrder: 0,
      total: 0,
    });
  });
});
