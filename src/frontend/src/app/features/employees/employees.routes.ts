import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const EMPLOYEES_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.EmployeesView)],
    data: { titleKey: 'employees.title' },
    loadComponent: () => import('./employees').then((m) => m.EmployeesPage),
  },
];
