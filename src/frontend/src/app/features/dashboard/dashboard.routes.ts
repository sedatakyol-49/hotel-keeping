import type { Routes } from '@angular/router';

export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    data: { titleKey: 'dashboard.title' },
    loadComponent: () => import('./dashboard').then((m) => m.DashboardPage),
  },
];
