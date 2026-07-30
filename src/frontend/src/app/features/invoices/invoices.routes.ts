import type { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { PERMISSIONS } from '../../core/models/permission.model';

/**
 * Faturalama rotalari.
 *
 * Izinler: okuma `Invoices.View`, taslak yazma/odeme `Invoices.Create`,
 * kesinlestirme `Invoices.Approve`, iptal `Invoices.Cancel` (aksiyon
 * seviyesinde, detay ekraninda kontrol edilir).
 *
 * **Silme rotasi yoktur**: fatura silinmez (GoBD §6.1/§6.4), duzeltme yalnizca
 * iptal faturasiyla yapilir. `new` sabit yolu `:id` kalibindan **once** gelir.
 */
export const INVOICES_ROUTES: Routes = [
  {
    path: '',
    canActivate: [permissionGuard(PERMISSIONS.InvoicesView)],
    data: { titleKey: 'invoices.title' },
    loadComponent: () => import('./invoice-list').then((m) => m.InvoiceListPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(PERMISSIONS.InvoicesCreate)],
    data: { titleKey: 'invoices.form.createTitle' },
    loadComponent: () => import('./invoice-form').then((m) => m.InvoiceFormPage),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard(PERMISSIONS.InvoicesCreate)],
    data: { titleKey: 'invoices.form.editTitle' },
    loadComponent: () => import('./invoice-form').then((m) => m.InvoiceFormPage),
  },
  {
    path: ':id',
    canActivate: [permissionGuard(PERMISSIONS.InvoicesView)],
    data: { titleKey: 'invoices.detail.title' },
    loadComponent: () => import('./invoice-detail').then((m) => m.InvoiceDetailPage),
  },
];
