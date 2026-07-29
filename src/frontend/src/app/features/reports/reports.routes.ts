import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const REPORTS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.ReportsView)],
    data: { titleKey: 'reports.title' },
    loadComponent: () => import('./reports').then((m) => m.ReportsPage),
  },
];
