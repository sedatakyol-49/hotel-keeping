import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import type { AuthenticatedUser } from '../models/auth.model';
import { PERMISSIONS } from '../models/permission.model';
import { AuthStore } from './auth.store';

function createUser(overrides: Partial<AuthenticatedUser> = {}): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'anna.becker@hotel.de',
    firstName: 'Anna',
    lastName: 'Becker',
    culture: 'de',
    headOfficeId: 'ho-1',
    roles: ['Receptionist'],
    permissions: [PERMISSIONS.ReservationsView, PERMISSIONS.RoomsView],
    hotels: [
      { id: 'h-1', name: 'Hotel Adler', city: 'München', country: 'DE', currency: 'EUR' },
      { id: 'h-2', name: 'Hotel Krone', city: 'Wien', country: 'AT', currency: 'EUR' },
    ],
    canAccessAllHotels: false,
    defaultHotelId: 'h-2',
    ...overrides,
  };
}

describe('AuthStore', () => {
  let store: AuthStore;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    store = TestBed.inject(AuthStore);
  });

  it('baslangicta anonim ve cozumlenmemis durumdadir', () => {
    expect(store.isAuthenticated()).toBe(false);
    expect(store.status()).toBe('unknown');
    expect(store.isResolved()).toBe(false);
    expect(store.permissions().size).toBe(0);
  });

  it('oturum kurulunca izinleri ve gorunen adi turetir', () => {
    store.setSession(createUser());

    expect(store.isAuthenticated()).toBe(true);
    expect(store.status()).toBe('authenticated');
    expect(store.displayName()).toBe('Anna Becker');
    expect(store.initials()).toBe('AB');
    expect(store.hasPermission(PERMISSIONS.RoomsView)).toBe(true);
    expect(store.hasPermission(PERMISSIONS.InvoicesApprove)).toBe(false);
  });

  it('tercih edilen otel yoksa varsayilan oteli aktif yapar', () => {
    store.setSession(createUser());

    expect(store.activeHotelId()).toBe('h-2');
    expect(store.activeHotel()?.name).toBe('Hotel Krone');
  });

  it('tercih edilen otel gecerliyse onu secer', () => {
    store.setSession(createUser(), 'h-1');

    expect(store.activeHotelId()).toBe('h-1');
  });

  it('erisilemeyen otel secimini reddeder ve durumu korur', () => {
    store.setSession(createUser(), 'h-1');

    expect(store.setActiveHotel('h-999')).toBe(false);
    expect(store.activeHotelId()).toBe('h-1');
  });

  it('konsolide gorunume yalnizca Head Office kullanicisi gecebilir', () => {
    store.setSession(createUser());
    expect(store.setActiveHotel(null)).toBe(false);

    store.setSession(createUser({ canAccessAllHotels: true }));
    expect(store.setActiveHotel(null)).toBe(true);
    expect(store.activeHotelId()).toBeNull();
    expect(store.canAccessHotel('baska-otel')).toBe(true);
  });

  it('any/all izin modlarini ayirt eder', () => {
    store.setSession(createUser());

    const keys = [PERMISSIONS.RoomsView, PERMISSIONS.InvoicesApprove];
    expect(store.matchesPermissions(keys, 'any')).toBe(true);
    expect(store.matchesPermissions(keys, 'all')).toBe(false);
    // Bos liste = kisitlama yok.
    expect(store.matchesPermissions([], 'all')).toBe(true);
  });

  it('cikista tum oturum verisini temizler', () => {
    store.setSession(createUser(), 'h-1');
    store.clear();

    expect(store.isAuthenticated()).toBe(false);
    expect(store.status()).toBe('anonymous');
    expect(store.activeHotelId()).toBeNull();
    expect(store.hotels()).toEqual([]);
  });

  it('hata anahtarini saklar ve temizler', () => {
    store.setAnonymous('auth.invalidCredentials');
    expect(store.errorKey()).toBe('auth.invalidCredentials');

    store.clearError();
    expect(store.errorKey()).toBeNull();
  });
});
