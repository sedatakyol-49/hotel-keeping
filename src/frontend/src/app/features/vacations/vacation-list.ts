import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { PERMISSIONS } from '../../core/models/permission.model';
import {
  VACATION_LIMITS,
  VACATION_STATUSES,
  VACATION_STATUS_LABEL_KEYS,
  isCancellable,
  isDecidable,
  isVacationStatus,
  type VacationListQuery,
  type VacationRequestResponse,
} from '../../core/models/vacation.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { isIsoDate } from '../../shared/forms/date-validators';
import { parseInteger } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { Badge } from '../../shared/ui/badge/badge';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { EmployeeOptionsStore } from '../employees/employee-options.store';
import { VacationStatusBadge } from './vacation-status';
import {
  VACATION_PAGE_SIZE_OPTIONS,
  VACATION_YEAR_MAX,
  VACATION_YEAR_MIN,
  parseVacationListQuery,
  vacationListQueryToParams,
  withVacationFilterChange,
} from './vacation-list-query';
import { VacationsStore, type VacationDecision } from './vacations.store';

/** Satir uzerinde acik olan karar paneli. */
interface PendingDecision {
  readonly id: string;
  readonly decision: VacationDecision;
}

/**
 * Izin talepleri (`GET /vacations`) + bakiye paneli (`GET /vacations/balances`).
 *
 * Filtreler ve sayfa **URL sorgu parametrelerinde** tutulur; masaustunde yogun
 * tablo, mobilde kart listesi — ikisi de ayni signal store'u okur.
 *
 * RBAC: karar aksiyonlari (`approve`/`reject`) yalnizca `Vacations.Approve`
 * ile, iptal `Vacations.Request` ile de gorunur. Karara baglanmis talepte
 * karar aksiyonlari hic render edilmez (sunucu da 409 dondururdu).
 */
@Component({
  selector: 'hc-vacation-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
    Badge,
    VacationStatusBadge,
    HasPermissionDirective,
  ],
  templateUrl: './vacation-list.html',
})
export class VacationListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(VacationsStore);
  protected readonly employees = inject(EmployeeOptionsStore);

  protected readonly approvePermission = PERMISSIONS.VacationsApprove;
  protected readonly requestPermission = PERMISSIONS.VacationsRequest;
  /**
   * Iptal iki alternatifli izindir (sunucu tarafinda tek policy ile ifade
   * edilemedigi icin handler'da kontrol edilir): `Vacations.Approve` her talebi,
   * `Vacations.Request` yalnizca **kendi** talebini iptal edebilir. Baskasinin
   * talebini iptal denemesi 403 doner ve `vacations.decide.forbidden` mesajina
   * cevrilir.
   */
  protected readonly cancelPermissions = [
    PERMISSIONS.VacationsRequest,
    PERMISSIONS.VacationsApprove,
  ];
  protected readonly statuses = VACATION_STATUSES;
  protected readonly statusLabelKeys = VACATION_STATUS_LABEL_KEYS;
  protected readonly pageSizeOptions = VACATION_PAGE_SIZE_OPTIONS;
  protected readonly yearMin = VACATION_YEAR_MIN;
  protected readonly yearMax = VACATION_YEAR_MAX;
  protected readonly noteMaxLength = VACATION_LIMITS.decisionNoteMaxLength;

  /** Satir bazli karar paneli — ayri bir diyalog bileseni gerekmez. */
  protected readonly pendingDecision = signal<PendingDecision | null>(null);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi; gecersiz degerler varsayilana duser. */
  protected readonly query = computed(() => parseVacationListQuery(this.queryParams()));

  /** Karar hatasini anlamli bir mesaja cevirir (409 = artik karar verilemez). */
  protected readonly actionErrorKey = computed(() => {
    const error = this.store.actionError();
    if (!error) {
      return null;
    }
    if (error.status === 409) {
      return 'vacations.decide.conflict';
    }
    if (error.status === 403) {
      return 'vacations.decide.forbidden';
    }
    return error.messageKey;
  });

  constructor() {
    // Sorgu degistikce liste ve bakiye birlikte yenilenir (ilk yukleme dahil).
    effect(() => {
      const query = this.query();
      this.pendingDecision.set(null);
      void this.store.load(query);
      void this.store.loadBalances({ employeeId: query.employeeId, year: query.year });
    });

    // Filtre ve bakiye panelindeki adlar icin kadro (`Employees.View` gerekir;
    // izin yoksa istek 403 doner ve ekran filtreyi gizler).
    void this.employees.load();
  }

  protected applyFromForm(
    employeeId: string,
    status: string,
    year: string,
    from: string,
    to: string,
  ): void {
    const parsedYear = parseInteger(year);
    this.applyFilters({
      employeeId: employeeId || null,
      status: isVacationStatus(status) ? status : null,
      year:
        parsedYear !== null && parsedYear >= this.yearMin && parsedYear <= this.yearMax
          ? parsedYear
          : null,
      from: isIsoDate(from) ? from : null,
      to: isIsoDate(to) ? to : null,
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
      employeeId: null,
      status: null,
      year: null,
      from: null,
      to: null,
    });
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

  protected retryBalances(): void {
    void this.store.reloadBalances();
  }

  protected canDecide(request: VacationRequestResponse): boolean {
    return isDecidable(request.status);
  }

  protected canCancel(request: VacationRequestResponse): boolean {
    return isCancellable(request.status);
  }

  protected isPanelOpen(request: VacationRequestResponse, decision: VacationDecision): boolean {
    const pending = this.pendingDecision();
    return pending?.id === request.id && pending.decision === decision;
  }

  protected hasOpenPanel(request: VacationRequestResponse): boolean {
    return this.pendingDecision()?.id === request.id;
  }

  /** Ret/iptal gerekcesi icin panel acar; onay tek tiklamada yurutulur. */
  protected openPanel(request: VacationRequestResponse, decision: VacationDecision): void {
    this.store.clearActionError();
    this.pendingDecision.set({ id: request.id, decision });
  }

  protected closePanel(): void {
    this.pendingDecision.set(null);
  }

  protected async decide(
    decision: VacationDecision,
    request: VacationRequestResponse,
    note = '',
  ): Promise<void> {
    const error = await this.store.decide(decision, request.id, note.trim() || null);
    if (error === null) {
      this.pendingDecision.set(null);
    }
  }

  /** Filtre degisikligi her zaman ilk sayfaya doner. */
  private applyFilters(changes: Partial<Omit<VacationListQuery, 'page'>>): void {
    void this.navigate(withVacationFilterChange(this.query(), changes));
  }

  private navigate(query: VacationListQuery): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: vacationListQueryToParams(query),
    });
  }
}
