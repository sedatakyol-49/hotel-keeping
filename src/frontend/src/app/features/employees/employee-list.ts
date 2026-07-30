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

import {
  EMPLOYMENT_TYPES,
  EMPLOYMENT_TYPE_LABEL_KEYS,
  isEmploymentType,
  type EmployeeListQuery,
  type EmployeeResponse,
} from '../../core/models/employee.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { parseInteger } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { Badge } from '../../shared/ui/badge/badge';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { DepartmentsStore } from './departments.store';
import {
  EMPLOYEE_PAGE_SIZE_OPTIONS,
  employeeListQueryToParams,
  parseEmployeeListQuery,
  withEmployeeFilterChange,
} from './employee-list-query';
import { EmployeesStore } from './employees.store';

/**
 * Calisan listesi (`GET /employees`).
 *
 * Filtreler ve sayfa **URL sorgu parametrelerinde** tutulur: sayfa yenilenince
 * durum korunur, baglanti paylasilabilir ve geri/ileri dugmeleri dogru calisir.
 * Masaustunde yogun tablo, mobilde kart listesi — ikisi de ayni signal store'u
 * okur. Isten ayrilmis kayitlar (`isActive === false`) gorsel olarak geri plana
 * cekilir; veri gizlenmez.
 */
@Component({
  selector: 'hc-employee-list',
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
    HasPermissionDirective,
  ],
  templateUrl: './employee-list.html',
})
export class EmployeeListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(EmployeesStore);
  protected readonly departments = inject(DepartmentsStore);

  protected readonly editPermission = PERMISSIONS.EmployeesEdit;
  protected readonly employmentTypes = EMPLOYMENT_TYPES;
  protected readonly employmentTypeLabelKeys = EMPLOYMENT_TYPE_LABEL_KEYS;
  protected readonly pageSizeOptions = EMPLOYEE_PAGE_SIZE_OPTIONS;

  /** Satir bazli silme onayi — ayri bir diyalog bileseni gerekmez. */
  protected readonly pendingDeleteId = signal<string | null>(null);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** URL tek dogruluk kaynagi; gecersiz degerler varsayilana duser. */
  protected readonly query = computed(() => parseEmployeeListQuery(this.queryParams()));

  protected readonly deleteErrorKey = computed(() => {
    const error = this.store.deleteError();
    if (!error) {
      return null;
    }
    return error.status === 409 ? 'employees.delete.conflict' : error.messageKey;
  });

  constructor() {
    // Sorgu degistikce liste yenilenir (ilk yukleme dahil).
    effect(() => {
      const query = this.query();
      this.pendingDeleteId.set(null);
      void this.store.load(query);
    });

    // Departman filtresi icin secenekler (`Employees.View` yeterlidir).
    void this.departments.load();
  }

  protected applyFromForm(
    search: string,
    departmentId: string,
    employmentType: string,
    includeTerminated: boolean,
  ): void {
    this.applyFilters({
      search: search.trim() || null,
      departmentId: departmentId || null,
      employmentType: isEmploymentType(employmentType) ? employmentType : null,
      includeTerminated,
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
      departmentId: null,
      employmentType: null,
      search: null,
      includeTerminated: false,
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

  protected requestDelete(id: string): void {
    this.store.clearDeleteError();
    this.pendingDeleteId.set(id);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected async confirmDelete(employee: EmployeeResponse): Promise<void> {
    const error = await this.store.remove(employee.id);
    if (error === null) {
      this.pendingDeleteId.set(null);
    }
  }

  /** Filtre degisikligi her zaman ilk sayfaya doner. */
  private applyFilters(changes: Partial<Omit<EmployeeListQuery, 'page'>>): void {
    void this.navigate(withEmployeeFilterChange(this.query(), changes));
  }

  private navigate(query: EmployeeListQuery): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: employeeListQueryToParams(query),
    });
  }
}
