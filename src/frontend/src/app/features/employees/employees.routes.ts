import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

/**
 * Personel modulu rotalari.
 *
 * Okuma `Employees.View`, yazma `Employees.Edit` gerektirir (sozlesme:
 * `GET /departments` de `Employees.View` ile calisir, bu yuzden departman
 * listesi okuma izniyle acilir; yazma aksiyonlari ekranda gizlenir).
 *
 * Sira onemlidir: sabit `departments/*` yollari `:id/edit` kalibindan once gelir.
 */
export const EMPLOYEES_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.EmployeesView)],
    data: { titleKey: 'employees.title' },
    loadComponent: () => import('./employee-list').then((m) => m.EmployeeListPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(PERMISSIONS.EmployeesEdit)],
    data: { titleKey: 'employees.form.createTitle' },
    loadComponent: () => import('./employee-form').then((m) => m.EmployeeFormPage),
  },
  {
    path: 'departments',
    canActivate: [permissionGuard(PERMISSIONS.EmployeesView)],
    data: { titleKey: 'employees.departments.title' },
    loadComponent: () => import('./department-list').then((m) => m.DepartmentListPage),
  },
  {
    path: 'departments/new',
    canActivate: [permissionGuard(PERMISSIONS.EmployeesEdit)],
    data: { titleKey: 'employees.departments.form.createTitle' },
    loadComponent: () => import('./department-form').then((m) => m.DepartmentFormPage),
  },
  {
    path: 'departments/:id/edit',
    canActivate: [permissionGuard(PERMISSIONS.EmployeesEdit)],
    data: { titleKey: 'employees.departments.form.editTitle' },
    loadComponent: () => import('./department-form').then((m) => m.DepartmentFormPage),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard(PERMISSIONS.EmployeesEdit)],
    data: { titleKey: 'employees.form.editTitle' },
    loadComponent: () => import('./employee-form').then((m) => m.EmployeeFormPage),
  },
];
