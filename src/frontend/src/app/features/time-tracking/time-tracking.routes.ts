import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const TIME_TRACKING_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.TimeTrackingView)],
    data: { titleKey: 'timeTracking.title' },
    loadComponent: () => import('./time-tracking').then((m) => m.TimeTrackingPage),
  },
];
