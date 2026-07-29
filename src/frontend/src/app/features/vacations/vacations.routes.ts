import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const VACATIONS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.VacationsView)],
    data: { titleKey: 'vacations.title' },
    loadComponent: () => import('./vacations').then((m) => m.VacationsPage),
  },
];
