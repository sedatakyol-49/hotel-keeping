import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import type { DepartmentResponse } from '../../core/models/employee.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { DepartmentsStore } from './departments.store';

/**
 * Departman listesi (`GET /departments`).
 *
 * Okuma `Employees.View` ile mumkundur; yazma aksiyonlari `Employees.Edit`
 * yoksa DOM'a hic basilmaz. Silme **hard delete** oldugu icin satir ici onay
 * zorunludur ve bagli calisan varken backend 409 doner — bu durum ayri bir
 * mesajla anlatilir (`employees.departments.delete.conflict`).
 */
@Component({
  selector: 'hc-department-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
    HasPermissionDirective,
  ],
  templateUrl: './department-list.html',
})
export class DepartmentListPage {
  protected readonly store = inject(DepartmentsStore);

  protected readonly editPermission = PERMISSIONS.EmployeesEdit;
  protected readonly pendingDeleteId = signal<string | null>(null);

  protected readonly deleteErrorKey = computed(() => {
    const error = this.store.deleteError();
    if (!error) {
      return null;
    }
    // 409 = bagli calisan var (sozlesmede tek 409 sebebi budur).
    return error.status === 409 ? 'employees.departments.delete.conflict' : error.messageKey;
  });

  constructor() {
    // Yazma islemlerinden sonra donuldugu icin her acilista tazelenir.
    void this.store.load(true);
  }

  protected retry(): void {
    void this.store.load(true);
  }

  protected requestDelete(id: string): void {
    this.store.clearDeleteError();
    this.pendingDeleteId.set(id);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected async confirmDelete(department: DepartmentResponse): Promise<void> {
    const error = await this.store.remove(department.id);
    if (error === null) {
      this.pendingDeleteId.set(null);
    }
  }
}
