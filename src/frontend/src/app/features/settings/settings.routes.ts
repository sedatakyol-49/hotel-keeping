import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.SettingsManage)],
    data: { titleKey: 'settings.title' },
    loadComponent: () => import('./settings').then((m) => m.SettingsPage),
  },
];
