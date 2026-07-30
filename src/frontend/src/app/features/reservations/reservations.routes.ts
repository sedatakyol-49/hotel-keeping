import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

/**
 * Rezervasyon modulu rotalari.
 *
 * Izinler: okuma `Reservations.View`, yazma `Reservations.Create`;
 * fiyat planlari ayri bir anahtar cifti kullanir (`Rates.View` / `Rates.Manage`).
 * Misafir icin ayri izin **yoktur** (sozlesme karari): misafir verisi rezervasyon
 * modulunun parcasidir.
 *
 * **Sira onemlidir**: sabit yollar (`new`, `occupancy`, `guests/*`,
 * `rate-plans/*`) `:id` kalibindan **once** gelir; aksi halde `/reservations/new`
 * bir rezervasyon kimligi sanilirdi.
 */
export const RESERVATIONS_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsView)],
    data: { titleKey: 'reservations.title' },
    loadComponent: () => import('./reservation-list').then((m) => m.ReservationListPage),
  },
  {
    path: 'occupancy',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsView)],
    data: { titleKey: 'occupancy.title' },
    loadComponent: () => import('./occupancy-plan').then((m) => m.OccupancyPlanPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsCreate)],
    data: { titleKey: 'reservations.wizard.title' },
    loadComponent: () => import('./reservation-wizard').then((m) => m.ReservationWizardPage),
  },
  {
    path: 'guests',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsView)],
    data: { titleKey: 'guests.title' },
    loadComponent: () => import('./guest-list').then((m) => m.GuestListPage),
  },
  {
    path: 'guests/new',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsCreate)],
    data: { titleKey: 'guests.form.createTitle' },
    loadComponent: () => import('./guest-form').then((m) => m.GuestFormPage),
  },
  {
    path: 'guests/:id/edit',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsCreate)],
    data: { titleKey: 'guests.form.editTitle' },
    loadComponent: () => import('./guest-form').then((m) => m.GuestFormPage),
  },
  {
    path: 'rate-plans',
    canActivate: [permissionGuard(PERMISSIONS.RatesView)],
    data: { titleKey: 'ratePlans.title' },
    loadComponent: () => import('./rate-plan-list').then((m) => m.RatePlanListPage),
  },
  {
    path: 'rate-plans/new',
    canActivate: [permissionGuard(PERMISSIONS.RatesManage)],
    data: { titleKey: 'ratePlans.form.createTitle' },
    loadComponent: () => import('./rate-plan-form').then((m) => m.RatePlanFormPage),
  },
  {
    path: 'rate-plans/:id/edit',
    canActivate: [permissionGuard(PERMISSIONS.RatesManage)],
    data: { titleKey: 'ratePlans.form.editTitle' },
    loadComponent: () => import('./rate-plan-form').then((m) => m.RatePlanFormPage),
  },
  {
    path: ':id',
    canActivate: [permissionGuard(PERMISSIONS.ReservationsView)],
    data: { titleKey: 'reservations.detail.title' },
    loadComponent: () => import('./reservation-detail').then((m) => m.ReservationDetailPage),
  },
];
