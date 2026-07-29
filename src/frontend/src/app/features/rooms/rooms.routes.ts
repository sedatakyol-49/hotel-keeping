import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const ROOMS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.RoomsView)],
    data: { titleKey: 'rooms.title' },
    loadComponent: () => import('./rooms').then((m) => m.RoomsPage),
  },
];
