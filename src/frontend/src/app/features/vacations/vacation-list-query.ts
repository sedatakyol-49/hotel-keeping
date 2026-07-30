import type { ParamMap, Params } from '@angular/router';

import { isVacationStatus, type VacationListQuery } from '../../core/models/vacation.model';
import { isIsoDate } from '../../shared/forms/date-validators';
import { parseInteger } from '../../shared/forms/numeric-validators';

/** Sayfa boyutu secenekleri — URL'de yalnizca bu degerler kabul edilir. */
export const VACATION_PAGE_SIZE_OPTIONS = [20, 50, 100] as const;

/** Sunucu dogrulamasiyla ayni yil araligi (`ListVacationsValidator`). */
export const VACATION_YEAR_MIN = 2000;
export const VACATION_YEAR_MAX = 2100;

export const DEFAULT_VACATION_LIST_QUERY: VacationListQuery = {
  page: 1,
  pageSize: VACATION_PAGE_SIZE_OPTIONS[0],
  employeeId: null,
  status: null,
  year: null,
  from: null,
  to: null,
};

/**
 * URL sorgu parametreleri -> `VacationListQuery`.
 *
 * URL tek dogruluk kaynagidir: sayfa yenilendiginde filtreler korunur, adres
 * paylasilabilir. Gecersiz/bilinmeyen degerler sessizce varsayilana duser.
 * Ters tarih araligi (`to < from`) sunucuda 400 uretecegi icin burada `to`
 * dusurulur — kullanici elle duzenlenmis bir adresle ekrani kiramaz.
 */
export function parseVacationListQuery(params: ParamMap): VacationListQuery {
  const page = parseInteger(params.get('page'));
  const pageSize = parseInteger(params.get('pageSize'));
  const year = parseInteger(params.get('year'));
  const employeeId = params.get('employeeId')?.trim();
  const status = params.get('status');
  const from = params.get('from')?.trim();
  const to = params.get('to')?.trim();

  const validFrom = from && isIsoDate(from) ? from : null;
  const validTo = to && isIsoDate(to) ? to : null;

  return {
    page: page !== null && page >= 1 ? page : DEFAULT_VACATION_LIST_QUERY.page,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_VACATION_LIST_QUERY.pageSize,
    employeeId: employeeId ? employeeId : null,
    status: isVacationStatus(status) ? status : null,
    year: year !== null && year >= VACATION_YEAR_MIN && year <= VACATION_YEAR_MAX ? year : null,
    from: validFrom,
    to: validFrom !== null && validTo !== null && validTo < validFrom ? null : validTo,
  };
}

/**
 * `VacationListQuery` -> URL sorgu parametreleri.
 * Varsayilan degerler adres cubugunu kirletmesin diye yazilmaz.
 */
export function vacationListQueryToParams(query: VacationListQuery): Params {
  const params: Params = {};

  if (query.page > 1) {
    params['page'] = query.page;
  }
  if (query.pageSize !== DEFAULT_VACATION_LIST_QUERY.pageSize) {
    params['pageSize'] = query.pageSize;
  }
  if (query.employeeId) {
    params['employeeId'] = query.employeeId;
  }
  if (query.status) {
    params['status'] = query.status;
  }
  if (query.year !== null && query.year !== undefined) {
    params['year'] = query.year;
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
export function withVacationFilterChange(
  query: VacationListQuery,
  changes: Partial<Omit<VacationListQuery, 'page'>>,
): VacationListQuery {
  return { ...query, ...changes, page: 1 };
}

/** Sayfalama disinda en az bir filtre aktif mi (bos durum metnini secmek icin). */
export function hasActiveVacationFilters(query: VacationListQuery): boolean {
  return Boolean(
    query.employeeId ||
    query.status ||
    (query.year !== null && query.year !== undefined) ||
    query.from ||
    query.to,
  );
}

function isPageSize(value: number | null): value is (typeof VACATION_PAGE_SIZE_OPTIONS)[number] {
  return value !== null && (VACATION_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}
