import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';

import { AuthStore } from '../state/auth.store';

/**
 * Korunan rotalar icin oturum kontrolu.
 *
 * Oturum geri yukleme uygulama acilisinda (`provideAppInitializer`) tamamlandigi
 * icin guard senkron calisabilir; `redirectTo` sorgu parametresi ile kullanici
 * giristen sonra geldigi sayfaya doner.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  if (authStore.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], {
    queryParams: state.url && state.url !== '/' ? { redirectTo: state.url } : undefined,
  });
};

/**
 * Yalnizca anonim kullanicilar icin (login sayfasi).
 * Oturum aciksa uygulamaya geri yonlendirir.
 */
export const guestGuard: CanActivateFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);

  return authStore.isAuthenticated() ? router.createUrlTree(['/dashboard']) : true;
};
