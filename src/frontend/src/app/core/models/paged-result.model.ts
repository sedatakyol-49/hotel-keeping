/**
 * Sayfalama sozlesmesi (api-contracts.md — Genel Kurallar):
 * istek `?page=1&pageSize=20`, yanit `{ items, page, pageSize, totalCount }`.
 */
export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface PageRequest {
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_PAGE_REQUEST: PageRequest = { page: 1, pageSize: 20 };

export function totalPages(result: Pick<PagedResult<unknown>, 'pageSize' | 'totalCount'>): number {
  return result.pageSize > 0 ? Math.ceil(result.totalCount / result.pageSize) : 0;
}
