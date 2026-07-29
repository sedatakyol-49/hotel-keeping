import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const RESERVATIONS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsView)],
    data: { titleKey: 'reservations.title' },
    loadComponent: () => import('./reservations').then((m) => m.ReservationsPage),
  },
];
