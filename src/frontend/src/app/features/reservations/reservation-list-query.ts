import type { ParamMap, Params } from '@angular/router';

import {
  isReservationChannel,
  isReservationStatus,
  type ReservationListQuery,
} from '../../core/models/reservation.model';
import { isIsoDate } from '../../shared/forms/date-validators';
import { parseInteger } from '../../shared/forms/numeric-validators';

/** Sayfa boyutu secenekleri — URL'de yalnizca bu degerler kabul edilir. */
export const RESERVATION_PAGE_SIZE_OPTIONS = [20, 50, 100] as const;

export const DEFAULT_RESERVATION_LIST_QUERY: ReservationListQuery = {
  page: 1,
  pageSize: RESERVATION_PAGE_SIZE_OPTIONS[0],
  status: null,
  channel: null,
  roomId: null,
  guestId: null,
  from: null,
  to: null,
  search: null,
};

/**
 * URL sorgu parametreleri -> `ReservationListQuery`.
 *
 * URL tek dogruluk kaynagidir: sayfa yenilendiginde filtreler korunur, adres
 * paylasilabilir. Gecersiz/bilinmeyen degerler sessizce varsayilana duser.
 *
 * Ters tarih araligi (`to < from`) sunucuda anlamsiz bir kesisim uretecegi icin
 * burada `to` dusurulur — kullanici elle duzenlenmis bir adresle ekrani kiramaz.
 * Not: liste filtresinde `from`/`to` **kesisen** konaklamalari secer
 * (`from < checkOut && checkIn < to`), kapsayanlari degil.
 */
export function parseReservationListQuery(params: ParamMap): ReservationListQuery {
  const page = parseInteger(params.get('page'));
  const pageSize = parseInteger(params.get('pageSize'));
  const status = params.get('status');
  const channel = params.get('channel');
  const roomId = params.get('roomId')?.trim();
  const guestId = params.get('guestId')?.trim();
  const from = params.get('from')?.trim();
  const to = params.get('to')?.trim();
  const search = params.get('search')?.trim();

  const validFrom = from && isIsoDate(from) ? from : null;
  const validTo = to && isIsoDate(to) ? to : null;

  return {
    page: page !== null && page >= 1 ? page : DEFAULT_RESERVATION_LIST_QUERY.page,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_RESERVATION_LIST_QUERY.pageSize,
    status: isReservationStatus(status) ? status : null,
    channel: isReservationChannel(channel) ? channel : null,
    roomId: roomId ? roomId : null,
    guestId: guestId ? guestId : null,
    from: validFrom,
    to: validFrom !== null && validTo !== null && validTo <= validFrom ? null : validTo,
    search: search ? search : null,
  };
}

/**
 * `ReservationListQuery` -> URL sorgu parametreleri.
 * Varsayilan degerler adres cubugunu kirletmesin diye yazilmaz.
 */
export function reservationListQueryToParams(query: ReservationListQuery): Params {
  const params: Params = {};

  if (query.page > 1) {
    params['page'] = query.page;
  }
  if (query.pageSize !== DEFAULT_RESERVATION_LIST_QUERY.pageSize) {
    params['pageSize'] = query.pageSize;
  }
  if (query.status) {
    params['status'] = query.status;
  }
  if (query.channel) {
    params['channel'] = query.channel;
  }
  if (query.roomId) {
    params['roomId'] = query.roomId;
  }
  if (query.guestId) {
    params['guestId'] = query.guestId;
  }
  if (query.from) {
    params['from'] = query.from;
  }
  if (query.to) {
    params['to'] = query.to;
  }
  const search = query.search?.trim();
  if (search) {
    params['search'] = search;
  }

  return params;
}

/** Filtre degisikligi her zaman ilk sayfaya doner. */
export function withReservationFilterChange(
  query: ReservationListQuery,
  changes: Partial<Omit<ReservationListQuery, 'page'>>,
): ReservationListQuery {
  return { ...query, ...changes, page: 1 };
}

/** Sayfalama disinda en az bir filtre aktif mi (bos durum metnini secmek icin). */
export function hasActiveReservationFilters(query: ReservationListQuery): boolean {
  return Boolean(
    query.status ||
    query.channel ||
    query.roomId ||
    query.guestId ||
    query.from ||
    query.to ||
    query.search?.trim(),
  );
}

function isPageSize(
  value: number | null,
): value is (typeof RESERVATION_PAGE_SIZE_OPTIONS)[number] {
  return value !== null && (RESERVATION_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}
