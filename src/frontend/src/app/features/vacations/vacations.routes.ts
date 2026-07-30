import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

/**
 * Izin (Urlaub) modulu rotalari.
 *
 * Okuma `Vacations.View`, talep olusturma `Vacations.Request` gerektirir.
 * Karar aksiyonlari ayri bir ekran degil, liste satirinin parcasidir ve
 * `Vacations.Approve` iznine gore gorunur/gizlenir.
 */
export const VACATIONS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.VacationsView)],
    data: { titleKey: 'vacations.title' },
    loadComponent: () => import('./vacation-list').then((m) => m.VacationListPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(PERMISSIONS.VacationsRequest)],
    data: { titleKey: 'vacations.form.createTitle' },
    loadComponent: () => import('./vacation-form').then((m) => m.VacationFormPage),
  },
];
