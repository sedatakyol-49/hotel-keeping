import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const SHIFTS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.ShiftsView)],
    data: { titleKey: 'shifts.title' },
    loadComponent: () => import('./shifts').then((m) => m.ShiftsPage),
  },
];
