import type { ParamMap, Params } from '@angular/router';

import {
  isHousekeepingStatus,
  ROOM_LIMITS,
  type RoomListQuery,
} from '../../core/models/room.model';
import { parseInteger } from '../../shared/forms/numeric-validators';

/** Sayfa boyutu secenekleri — URL'de yalnizca bu degerler kabul edilir. */
export const ROOM_PAGE_SIZE_OPTIONS = [20, 50, 100] as const;

export const DEFAULT_ROOM_LIST_QUERY: RoomListQuery = {
  page: 1,
  pageSize: ROOM_PAGE_SIZE_OPTIONS[0],
};

/**
 * URL sorgu parametreleri -> `RoomListQuery`.
 *
 * URL tek dogruluk kaynagidir: sayfa yenilendiginde filtreler korunur.
 * Gecersiz/bilinmeyen degerler sessizce varsayilana duser (kullanici elle
 * duzenlenmis bir adres ile ekrani kiramaz).
 */
export function parseRoomListQuery(params: ParamMap): RoomListQuery {
  const page = parseInteger(params.get('page'));
  const pageSize = parseInteger(params.get('pageSize'));
  const floor = parseInteger(params.get('floor'));
  const status = params.get('housekeepingStatus');
  const roomTypeId = params.get('roomTypeId')?.trim();
  const search = params.get('search')?.trim();

  return {
    page: page !== null && page >= 1 ? page : DEFAULT_ROOM_LIST_QUERY.page,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_ROOM_LIST_QUERY.pageSize,
    roomTypeId: roomTypeId ? roomTypeId : null,
    floor:
      floor !== null && floor >= ROOM_LIMITS.floorMin && floor <= ROOM_LIMITS.floorMax
        ? floor
        : null,
    housekeepingStatus: isHousekeepingStatus(status) ? status : null,
    search: search ? search : null,
  };
}

/**
 * `RoomListQuery` -> URL sorgu parametreleri.
 * Varsayilan degerler adres cubugunu kirletmesin diye yazilmaz.
 */
export function roomListQueryToParams(query: RoomListQuery): Params {
  const params: Params = {};

  if (query.page > 1) {
    params['page'] = query.page;
  }
  if (query.pageSize !== DEFAULT_ROOM_LIST_QUERY.pageSize) {
    params['pageSize'] = query.pageSize;
  }
  if (query.roomTypeId) {
    params['roomTypeId'] = query.roomTypeId;
  }
  if (query.floor !== null && query.floor !== undefined) {
    params['floor'] = query.floor;
  }
  if (query.housekeepingStatus) {
    params['housekeepingStatus'] = query.housekeepingStatus;
  }
  const search = query.search?.trim();
  if (search) {
    params['search'] = search;
  }

  return params;
}

/** Filtre degisikligi her zaman ilk sayfaya doner. */
export function withFilterChange(
  query: RoomListQuery,
  changes: Partial<Omit<RoomListQuery, 'page'>>,
): RoomListQuery {
  return { ...query, ...changes, page: 1 };
}

/** Sayfalama disinda en az bir filtre aktif mi (bos durum metnini secmek icin). */
export function hasActiveRoomFilters(query: RoomListQuery): boolean {
  return Boolean(
    query.roomTypeId ||
    query.housekeepingStatus ||
    query.search?.trim() ||
    (query.floor !== null && query.floor !== undefined),
  );
}

function isPageSize(value: number | null): value is (typeof ROOM_PAGE_SIZE_OPTIONS)[number] {
  return value !== null && (ROOM_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}
