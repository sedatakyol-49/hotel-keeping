import type { ParamMap, Params } from '@angular/router';

import type { GuestListQuery } from '../../core/models/guest.model';
import { parseInteger } from '../../shared/forms/numeric-validators';

/** Sayfa boyutu secenekleri — URL'de yalnizca bu degerler kabul edilir. */
export const GUEST_PAGE_SIZE_OPTIONS = [20, 50, 100] as const;

export const DEFAULT_GUEST_LIST_QUERY: GuestListQuery = {
  page: 1,
  pageSize: GUEST_PAGE_SIZE_OPTIONS[0],
  search: null,
};

/** URL sorgu parametreleri -> `GuestListQuery` (gecersiz deger varsayilana duser). */
export function parseGuestListQuery(params: ParamMap): GuestListQuery {
  const page = parseInteger(params.get('page'));
  const pageSize = parseInteger(params.get('pageSize'));
  const search = params.get('search')?.trim();

  return {
    page: page !== null && page >= 1 ? page : DEFAULT_GUEST_LIST_QUERY.page,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_GUEST_LIST_QUERY.pageSize,
    search: search ? search : null,
  };
}

/** Varsayilan degerler adres cubugunu kirletmesin diye yazilmaz. */
export function guestListQueryToParams(query: GuestListQuery): Params {
  const params: Params = {};

  if (query.page > 1) {
    params['page'] = query.page;
  }
  if (query.pageSize !== DEFAULT_GUEST_LIST_QUERY.pageSize) {
    params['pageSize'] = query.pageSize;
  }
  const search = query.search?.trim();
  if (search) {
    params['search'] = search;
  }

  return params;
}

/** Filtre degisikligi her zaman ilk sayfaya doner. */
export function withGuestFilterChange(
  query: GuestListQuery,
  changes: Partial<Omit<GuestListQuery, 'page'>>,
): GuestListQuery {
  return { ...query, ...changes, page: 1 };
}

export function hasActiveGuestFilters(query: GuestListQuery): boolean {
  return Boolean(query.search?.trim());
}

function isPageSize(value: number | null): value is (typeof GUEST_PAGE_SIZE_OPTIONS)[number] {
  return value !== null && (GUEST_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}
