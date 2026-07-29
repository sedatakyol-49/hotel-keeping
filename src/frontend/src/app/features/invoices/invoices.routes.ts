import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

export const INVOICES_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.InvoicesView)],
    data: { titleKey: 'invoices.title' },
    loadComponent: () => import('./invoices').then((m) => m.InvoicesPage),
  },
];
