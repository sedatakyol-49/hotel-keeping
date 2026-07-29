import { TestBed } from '@angular/core/testing';
import {
  Router,
  UrlTree,
  type ActivatedRouteSnapshot,
  type RouterStateSnapshot,
} from '@angular/router';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import type { AuthenticatedUser } from '../models/auth.model';
import { PERMISSIONS } from '../models/permission.model';
import { AuthStore } from '../state/auth.store';
import { permissionGuard } from './permission.guard';

const user: AuthenticatedUser = {
  id: 'u-1',
  email: 'klaus.meier@hotel.de',
  roles: ['Housekeeping'],
  permissions: [PERMISSIONS.HousekeepingView, PERMISSIONS.HousekeepingUpdate],
  hotels: [{ id: 'h-1', name: 'Hotel Adler' }],
  canAccessAllHotels: false,
  defaultHotelId: 'h-1',
};

function runGuard(
  guard: ReturnType<typeof permissionGuard>,
  routeData: Record<string, unknown> = {},
  url = '/invoices',
): boolean | UrlTree {
  const route = { data: routeData } as unknown as ActivatedRouteSnapshot;
  const state = { url } as RouterStateSnapshot;
  return TestBed.runInInjectionContext(() => guard(route, state) as boolean | UrlTree);
}

describe('permissionGuard', () => {
  let authStore: AuthStore;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    authStore = TestBed.inject(AuthStore);
    router = TestBed.inject(Router);
  });

  it('oturum yoksa redirectTo parametresiyle login sayfasina yonlendirir', () => {
    const result = runGuard(permissionGuard(PERMISSIONS.HousekeepingView));

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login?redirectTo=%2Finvoices');
  });

  it('gerekli izin varsa erisime izin verir', () => {
    authStore.setSession(user);

    expect(runGuard(permissionGuard(PERMISSIONS.HousekeepingView), {}, '/housekeeping')).toBe(true);
  });

  it('izin yoksa access-denied sayfasina yonlendirir', () => {
    authStore.setSession(user);

    const result = runGuard(permissionGuard(PERMISSIONS.InvoicesView));

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/access-denied');
  });

  it('izinleri rota data alanindan okuyabilir', () => {
    authStore.setSession(user);

    expect(
      runGuard(
        permissionGuard(),
        { permissions: [PERMISSIONS.HousekeepingUpdate] },
        '/housekeeping',
      ),
    ).toBe(true);
    expect(runGuard(permissionGuard(), { permissions: [PERMISSIONS.ReportsView] })).toBeInstanceOf(
      UrlTree,
    );
  });

  it('all modunda izinlerin tamami gereklidir', () => {
    authStore.setSession(user);

    const keys = [PERMISSIONS.HousekeepingView, PERMISSIONS.ReportsView];
    expect(runGuard(permissionGuard(keys, 'any'), {}, '/housekeeping')).toBe(true);
    expect(runGuard(permissionGuard(keys, 'all'), {}, '/housekeeping')).toBeInstanceOf(UrlTree);
  });

  it('izin listesi bos ise yalnizca oturum kontrolu yapar', () => {
    authStore.setSession(user);

    expect(runGuard(permissionGuard(), {}, '/dashboard')).toBe(true);
  });
});
