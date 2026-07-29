import { inject } from '@angular/core';
import { Router, type ActivatedRouteSnapshot, type CanActivateFn } from '@angular/router';

import type { PermissionKey, PermissionMatchMode } from '../models/permission.model';
import { AuthStore } from '../state/auth.store';

/** Rota `data` alani uzerinden izin tanimlamak icin sozlesme. */
export interface PermissionRouteData {
  readonly permissions?: readonly PermissionKey[];
  readonly permissionMode?: PermissionMatchMode;
}

/**
 * Izin anahtari bazli rota korumasi (mimari §7).
 *
 * Kullanim 1 — fabrika:
 * ```ts
 * { path: 'invoices', canActivate: [permissionGuard(PERMISSIONS.InvoicesView)] }
 * ```
 * Kullanim 2 — rota `data`'si:
 * ```ts
 * { path: 'invoices', canActivate: [permissionGuard()], data: { permissions: ['Invoices.View'] } }
 * ```
 *
 * Oturum yoksa `/login`, izin yoksa `/access-denied` adresine yonlendirir.
 */
export function permissionGuard(
  permissions?: PermissionKey | readonly PermissionKey[],
  mode: PermissionMatchMode = 'any',
): CanActivateFn {
  const declared = normalize(permissions);

  return (route, state) => {
    const authStore = inject(AuthStore);
    const router = inject(Router);

    if (!authStore.isAuthenticated()) {
      return router.createUrlTree(['/login'], {
        queryParams: state.url && state.url !== '/' ? { redirectTo: state.url } : undefined,
      });
    }

    const required = declared.length > 0 ? declared : readRoutePermissions(route);
    const effectiveMode = declared.length > 0 ? mode : (readRouteMode(route) ?? mode);

    if (authStore.matchesPermissions(required, effectiveMode)) {
      return true;
    }

    return router.createUrlTree(['/access-denied']);
  };
}

function normalize(
  permissions: PermissionKey | readonly PermissionKey[] | undefined,
): readonly PermissionKey[] {
  if (!permissions) {
    return [];
  }
  return Array.isArray(permissions) ? permissions : [permissions as PermissionKey];
}

function readRoutePermissions(route: ActivatedRouteSnapshot): readonly PermissionKey[] {
  const data = route.data as PermissionRouteData;
  return normalize(data.permissions);
}

function readRouteMode(route: ActivatedRouteSnapshot): PermissionMatchMode | null {
  const data = route.data as PermissionRouteData;
  return data.permissionMode ?? null;
}
