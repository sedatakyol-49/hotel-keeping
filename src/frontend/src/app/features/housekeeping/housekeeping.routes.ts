import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

/** Kat panosu — okuma `Housekeeping.View`, durum degisikligi `Housekeeping.Update`. */
export const HOUSEKEEPING_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.HousekeepingView)],
    data: { titleKey: 'housekeeping.title' },
    loadComponent: () => import('./housekeeping').then((m) => m.HousekeepingPage),
  },
];
