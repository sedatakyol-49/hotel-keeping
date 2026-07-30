import type { ParamMap, Params } from '@angular/router';

import { isEmploymentType, type EmployeeListQuery } from '../../core/models/employee.model';
import { parseInteger } from '../../shared/forms/numeric-validators';

/** Sayfa boyutu secenekleri — URL'de yalnizca bu degerler kabul edilir. */
export const EMPLOYEE_PAGE_SIZE_OPTIONS = [20, 50, 100] as const;

export const DEFAULT_EMPLOYEE_LIST_QUERY: EmployeeListQuery = {
  page: 1,
  pageSize: EMPLOYEE_PAGE_SIZE_OPTIONS[0],
  departmentId: null,
  employmentType: null,
  search: null,
  includeTerminated: false,
};

/**
 * URL sorgu parametreleri -> `EmployeeListQuery`.
 *
 * URL tek dogruluk kaynagidir: sayfa yenilendiginde filtreler korunur, adres
 * paylasilabilir. Gecersiz/bilinmeyen degerler sessizce varsayilana duser
 * (kullanici elle duzenlenmis bir adresle ekrani kiramaz).
 */
export function parseEmployeeListQuery(params: ParamMap): EmployeeListQuery {
  const page = parseInteger(params.get('page'));
  const pageSize = parseInteger(params.get('pageSize'));
  const departmentId = params.get('departmentId')?.trim();
  const employmentType = params.get('employmentType');
  const search = params.get('search')?.trim();

  return {
    page: page !== null && page >= 1 ? page : DEFAULT_EMPLOYEE_LIST_QUERY.page,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_EMPLOYEE_LIST_QUERY.pageSize,
    departmentId: departmentId ? departmentId : null,
    employmentType: isEmploymentType(employmentType) ? employmentType : null,
    search: search ? search : null,
    // Yalnizca acik `true` isten ayrilanlari getirir (sunucu varsayilani false).
    includeTerminated: params.get('includeTerminated') === 'true',
  };
}

/**
 * `EmployeeListQuery` -> URL sorgu parametreleri.
 * Varsayilan degerler adres cubugunu kirletmesin diye yazilmaz.
 */
export function employeeListQueryToParams(query: EmployeeListQuery): Params {
  const params: Params = {};

  if (query.page > 1) {
    params['page'] = query.page;
  }
  if (query.pageSize !== DEFAULT_EMPLOYEE_LIST_QUERY.pageSize) {
    params['pageSize'] = query.pageSize;
  }
  if (query.departmentId) {
    params['departmentId'] = query.departmentId;
  }
  if (query.employmentType) {
    params['employmentType'] = query.employmentType;
  }
  const search = query.search?.trim();
  if (search) {
    params['search'] = search;
  }
  if (query.includeTerminated) {
    params['includeTerminated'] = true;
  }

  return params;
}

/** Filtre degisikligi her zaman ilk sayfaya doner. */
export function withEmployeeFilterChange(
  query: EmployeeListQuery,
  changes: Partial<Omit<EmployeeListQuery, 'page'>>,
): EmployeeListQuery {
  return { ...query, ...changes, page: 1 };
}

/** Sayfalama disinda en az bir filtre aktif mi (bos durum metnini secmek icin). */
export function hasActiveEmployeeFilters(query: EmployeeListQuery): boolean {
  return Boolean(
    query.departmentId || query.employmentType || query.search?.trim() || query.includeTerminated,
  );
}

function isPageSize(value: number | null): value is (typeof EMPLOYEE_PAGE_SIZE_OPTIONS)[number] {
  return value !== null && (EMPLOYEE_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}
