import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import {
  INVOICE_STATUSES,
  INVOICE_STATUS_LABEL_KEYS,
  isInvoiceStatus,
  type InvoiceListQuery,
} from '../../core/models/invoice.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { isIsoDate } from '../../shared/forms/date-validators';
import { parseInteger } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { InvoiceStatusBadge } from './invoice-status';
import {
  INVOICE_PAGE_SIZE_OPTIONS,
  invoiceListQueryToParams,
  parseInvoiceListQuery,
  withInvoiceFilterChange,
} from './invoice-list-query';
import { InvoicesStore } from './invoices.store';

/**
 * Fatura listesi (`GET /invoices`).
 *
 * Filtreler ve sayfa **URL sorgu parametrelerinde** tutulur. Tutarlar `hcMoney`
 * ile locale'e uygun bicimlenir (`de-DE` → `1.234,56 €`).
 *
 * Taslakta numara **yoktur** (`invoiceNumber: null`): numara yalnizca finalize
 * aninda atanir ve terk edilen taslaklar sekansta bosluk birakmaz. Liste
 * numarasiz satirda bu durumu acikca yazar, bos hucre birakmaz.
 */
@Component({
  selector: 'hc-invoice-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    MoneyPipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
    InvoiceStatusBadge,
    HasPermissionDirective,
  ],
  templateUrl: './invoice-list.html',
})
export class InvoiceListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(InvoicesStore);

  protected readonly createPermission = PERMISSIONS.InvoicesCreate;
  protected readonly statuses = INVOICE_STATUSES;
  protected readonly statusLabelKeys = INVOICE_STATUS_LABEL_KEYS;
  protected readonly pageSizeOptions = INVOICE_PAGE_SIZE_OPTIONS;

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  protected readonly query = computed(() => parseInvoiceListQuery(this.queryParams()));

  constructor() {
    effect(() => {
      void this.store.load(this.query());
    });
  }

  protected applyFromForm(status: string, from: string, to: string, search: string): void {
    this.applyFilters({
      status: isInvoiceStatus(status) ? status : null,
      from: isIsoDate(from) ? from : null,
      to: isIsoDate(to) ? to : null,
      search: search.trim() || null,
    });
  }

  protected onPageSizeChange(event: Event): void {
    const pageSize = parseInteger((event.target as HTMLSelectElement).value);
    if (pageSize !== null) {
      this.applyFilters({ pageSize });
    }
  }

  protected resetFilters(): void {
    void this.navigate({
      ...this.query(),
      page: 1,
      status: null,
      guestId: null,
      reservationId: null,
      from: null,
      to: null,
      search: null,
    });
  }

  protected clearReservationFilter(): void {
    this.applyFilters({ reservationId: null });
  }

  protected clearGuestFilter(): void {
    this.applyFilters({ guestId: null });
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.store.totalPages()) {
      return;
    }
    void this.navigate({ ...this.query(), page });
  }

  protected retry(): void {
    void this.store.reload();
  }

  private applyFilters(changes: Partial<Omit<InvoiceListQuery, 'page'>>): void {
    void this.navigate(withInvoiceFilterChange(this.query(), changes));
  }

  private navigate(query: InvoiceListQuery): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: invoiceListQueryToParams(query),
    });
  }
}
