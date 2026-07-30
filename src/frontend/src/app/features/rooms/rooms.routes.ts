import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

/**
 * Oda yonetimi rotalari.
 *
 * Okuma `Rooms.View`, yazma ve oda tipi yonetimi `Rooms.Manage` gerektirir.
 * Sira onemlidir: sabit `types/*` yollari `:id/edit` kalibindan once gelir.
 */
export const ROOMS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.RoomsView)],
    data: { titleKey: 'rooms.title' },
    loadComponent: () => import('./room-list').then((m) => m.RoomListPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(PERMISSIONS.RoomsManage)],
    data: { titleKey: 'rooms.form.createTitle' },
    loadComponent: () => import('./room-form').then((m) => m.RoomFormPage),
  },
  {
    path: 'types',
    canActivate: [permissionGuard(PERMISSIONS.RoomsManage)],
    data: { titleKey: 'rooms.types.title' },
    loadComponent: () => import('./room-type-list').then((m) => m.RoomTypeListPage),
  },
  {
    path: 'types/new',
    canActivate: [permissionGuard(PERMISSIONS.RoomsManage)],
    data: { titleKey: 'rooms.types.form.createTitle' },
    loadComponent: () => import('./room-type-form').then((m) => m.RoomTypeFormPage),
  },
  {
    path: 'types/:id/edit',
    canActivate: [permissionGuard(PERMISSIONS.RoomsManage)],
    data: { titleKey: 'rooms.types.form.editTitle' },
    loadComponent: () => import('./room-type-form').then((m) => m.RoomTypeFormPage),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard(PERMISSIONS.RoomsManage)],
    data: { titleKey: 'rooms.form.editTitle' },
    loadComponent: () => import('./room-form').then((m) => m.RoomFormPage),
  },
];
