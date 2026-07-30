import type { ParamMap, Params } from '@angular/router';

import type { TimeEntryListQuery } from '../../core/models/time-entry.model';
import { isIsoDate } from '../../shared/forms/date-validators';
import { parseInteger } from '../../shared/forms/numeric-validators';

/** Sayfa boyutu secenekleri — URL'de yalnizca bu degerler kabul edilir. */
export const TIME_ENTRY_PAGE_SIZE_OPTIONS = [20, 50, 100] as const;

export const DEFAULT_TIME_ENTRY_LIST_QUERY: TimeEntryListQuery = {
  page: 1,
  pageSize: TIME_ENTRY_PAGE_SIZE_OPTIONS[0],
  employeeId: null,
  from: null,
  to: null,
};

/**
 * URL sorgu parametreleri -> `TimeEntryListQuery`.
 * URL tek dogruluk kaynagidir; gecersiz degerler sessizce varsayilana duser.
 * Ters aralikta (`to < from`) `to` dusurulur — sunucu 400 dondurmesin.
 */
export function parseTimeEntryListQuery(params: ParamMap): TimeEntryListQuery {
  const page = parseInteger(params.get('page'));
  const pageSize = parseInteger(params.get('pageSize'));
  const employeeId = params.get('employeeId')?.trim();
  const from = params.get('from')?.trim();
  const to = params.get('to')?.trim();

  const validFrom = from && isIsoDate(from) ? from : null;
  const validTo = to && isIsoDate(to) ? to : null;

  return {
    page: page !== null && page >= 1 ? page : DEFAULT_TIME_ENTRY_LIST_QUERY.page,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_TIME_ENTRY_LIST_QUERY.pageSize,
    employeeId: employeeId ? employeeId : null,
    from: validFrom,
    to: validFrom !== null && validTo !== null && validTo < validFrom ? null : validTo,
  };
}

/** Varsayilan degerler adres cubugunu kirletmesin diye yazilmaz. */
export function timeEntryListQueryToParams(query: TimeEntryListQuery): Params {
  const params: Params = {};

  if (query.page > 1) {
    params['page'] = query.page;
  }
  if (query.pageSize !== DEFAULT_TIME_ENTRY_LIST_QUERY.pageSize) {
    params['pageSize'] = query.pageSize;
  }
  if (query.employeeId) {
    params['employeeId'] = query.employeeId;
  }
  if (query.from) {
    params['from'] = query.from;
  }
  if (query.to) {
    params['to'] = query.to;
  }

  return params;
}

/** Filtre degisikligi her zaman ilk sayfaya doner. */
export function withTimeEntryFilterChange(
  query: TimeEntryListQuery,
  changes: Partial<Omit<TimeEntryListQuery, 'page'>>,
): TimeEntryListQuery {
  return { ...query, ...changes, page: 1 };
}

/** Sayfalama disinda en az bir filtre aktif mi. */
export function hasActiveTimeEntryFilters(query: TimeEntryListQuery): boolean {
  return Boolean(query.employeeId || query.from || query.to);
}

function isPageSize(value: number | null): value is (typeof TIME_ENTRY_PAGE_SIZE_OPTIONS)[number] {
  return value !== null && (TIME_ENTRY_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}
