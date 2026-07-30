import type { ParamMap, Params } from '@angular/router';

import { isInvoiceStatus, type InvoiceListQuery } from '../../core/models/invoice.model';
import { isIsoDate } from '../../shared/forms/date-validators';
import { parseInteger } from '../../shared/forms/numeric-validators';

/** Sayfa boyutu secenekleri — URL'de yalnizca bu degerler kabul edilir. */
export const INVOICE_PAGE_SIZE_OPTIONS = [20, 50, 100] as const;

export const DEFAULT_INVOICE_LIST_QUERY: InvoiceListQuery = {
  page: 1,
  pageSize: INVOICE_PAGE_SIZE_OPTIONS[0],
  status: null,
  guestId: null,
  reservationId: null,
  from: null,
  to: null,
  search: null,
};

/**
 * URL sorgu parametreleri -> `InvoiceListQuery`.
 *
 * Tarih araligi **`issuedAt`** uzerinde ve **her iki uc dahildir**; ters aralik
 * (`to < from`) sunucuda bos sonuc uretecegi icin burada `to` dusurulur.
 * Not: tarih filtresi verildiginde sunucu **taslaklari listelemez** (taslagin
 * fatura tarihi yoktur) — ekran bunu bilgi metniyle acikilar.
 */
export function parseInvoiceListQuery(params: ParamMap): InvoiceListQuery {
  const page = parseInteger(params.get('page'));
  const pageSize = parseInteger(params.get('pageSize'));
  const status = params.get('status');
  const guestId = params.get('guestId')?.trim();
  const reservationId = params.get('reservationId')?.trim();
  const from = params.get('from')?.trim();
  const to = params.get('to')?.trim();
  const search = params.get('search')?.trim();

  const validFrom = from && isIsoDate(from) ? from : null;
  const validTo = to && isIsoDate(to) ? to : null;

  return {
    page: page !== null && page >= 1 ? page : DEFAULT_INVOICE_LIST_QUERY.page,
    pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_INVOICE_LIST_QUERY.pageSize,
    status: isInvoiceStatus(status) ? status : null,
    guestId: guestId ? guestId : null,
    reservationId: reservationId ? reservationId : null,
    from: validFrom,
    to: validFrom !== null && validTo !== null && validTo < validFrom ? null : validTo,
    search: search ? search : null,
  };
}

/** Varsayilan degerler adres cubugunu kirletmesin diye yazilmaz. */
export function invoiceListQueryToParams(query: InvoiceListQuery): Params {
  const params: Params = {};

  if (query.page > 1) {
    params['page'] = query.page;
  }
  if (query.pageSize !== DEFAULT_INVOICE_LIST_QUERY.pageSize) {
    params['pageSize'] = query.pageSize;
  }
  if (query.status) {
    params['status'] = query.status;
  }
  if (query.guestId) {
    params['guestId'] = query.guestId;
  }
  if (query.reservationId) {
    params['reservationId'] = query.reservationId;
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
export function withInvoiceFilterChange(
  query: InvoiceListQuery,
  changes: Partial<Omit<InvoiceListQuery, 'page'>>,
): InvoiceListQuery {
  return { ...query, ...changes, page: 1 };
}

export function hasActiveInvoiceFilters(query: InvoiceListQuery): boolean {
  return Boolean(
    query.status ||
    query.guestId ||
    query.reservationId ||
    query.from ||
    query.to ||
    query.search?.trim(),
  );
}

/** Tarih filtresi aktif mi (taslaklarin listelenmedigi uyarisi icin). */
export function hasDateFilter(query: InvoiceListQuery): boolean {
  return Boolean(query.from || query.to);
}

function isPageSize(value: number | null): value is (typeof INVOICE_PAGE_SIZE_OPTIONS)[number] {
  return value !== null && (INVOICE_PAGE_SIZE_OPTIONS as readonly number[]).includes(value);
}
