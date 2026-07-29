import type { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { Shell } from './layout/shell/shell';

/**
 * Rota agaci. Tum ozellik modulleri lazy-load edilir; korumalar iki
 * katmanlidir: `authGuard` (oturum) + `permissionGuard` (izin anahtari).
 */
export const routes: Routes = [
  {
    path: 'login',
    loadChildren: () => import('./features/auth/login/login.routes').then((m) => m.LOGIN_ROUTES),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'rooms',
        loadChildren: () => import('./features/rooms/rooms.routes').then((m) => m.ROOMS_ROUTES),
      },
      {
        path: 'reservations',
        loadChildren: () =>
          import('./features/reservations/reservations.routes').then((m) => m.RESERVATIONS_ROUTES),
      },
      {
        path: 'housekeeping',
        loadChildren: () =>
          import('./features/housekeeping/housekeeping.routes').then((m) => m.HOUSEKEEPING_ROUTES),
      },
      {
        path: 'invoices',
        loadChildren: () =>
          import('./features/invoices/invoices.routes').then((m) => m.INVOICES_ROUTES),
      },
      {
        path: 'employees',
        loadChildren: () =>
          import('./features/employees/employees.routes').then((m) => m.EMPLOYEES_ROUTES),
      },
      {
        path: 'vacations',
        loadChildren: () =>
          import('./features/vacations/vacations.routes').then((m) => m.VACATIONS_ROUTES),
      },
      {
        path: 'time-tracking',
        loadChildren: () =>
          import('./features/time-tracking/time-tracking.routes').then(
            (m) => m.TIME_TRACKING_ROUTES,
          ),
      },
      {
        path: 'shifts',
        loadChildren: () => import('./features/shifts/shifts.routes').then((m) => m.SHIFTS_ROUTES),
      },
      {
        path: 'reports',
        loadChildren: () =>
          import('./features/reports/reports.routes').then((m) => m.REPORTS_ROUTES),
      },
      {
        path: 'settings',
        loadChildren: () =>
          import('./features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
      {
        path: 'access-denied',
        data: { titleKey: 'errors.accessDenied.title' },
        loadComponent: () =>
          import('./features/errors/access-denied').then((m) => m.AccessDeniedPage),
      },
      {
        path: '**',
        data: { titleKey: 'errors.pageNotFound.title' },
        loadComponent: () => import('./features/errors/not-found').then((m) => m.NotFoundPage),
      },
    ],
  },
];
