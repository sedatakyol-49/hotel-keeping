import type { Routes } from '@angular/router';

import {
  languageNegotiationGuard,
  languageRouteGuard,
  supportedLanguageMatch,
} from './core/i18n/language.guards';
import { LEGAL_DOCUMENTS } from './features/legal/legal-documents';
import { GuestShell } from './layout/guest-shell/guest-shell';

/**
 * Dil on ekli sayfalar. Bu tablo `/:lang` altinda yasar; segmentler dilden
 * bagimsizdir (bkz. legal-documents.ts icindeki slug gerekcesi).
 *
 * `data` alani SEO sozlesmesidir (`GuestSeoService` okur):
 *   titleKey / descriptionKey -> `<title>` ve `meta[name=description]`
 *   noindex                   -> `meta[name=robots] = noindex, follow`
 */
export const LANGUAGE_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    data: { titleKey: 'home.meta.title', descriptionKey: 'home.meta.description' },
    loadComponent: () => import('./features/home/home').then((m) => m.HomePage),
  },
  {
    path: 'search',
    // Sorgu bagimli: dizine eklenmez, ama baglantilari izlenir.
    data: {
      titleKey: 'search.meta.title',
      descriptionKey: 'search.meta.description',
      noindex: true,
    },
    loadComponent: () => import('./features/search/search').then((m) => m.SearchPage),
  },
  {
    path: 'rooms/:slug',
    data: { titleKey: 'roomType.meta.title', descriptionKey: 'roomType.meta.description' },
    loadComponent: () => import('./features/room-type/room-type').then((m) => m.RoomTypePage),
  },
  {
    path: 'booking',
    data: {
      titleKey: 'booking.meta.title',
      descriptionKey: 'booking.meta.description',
      noindex: true,
    },
    loadComponent: () => import('./features/booking/booking').then((m) => m.BookingPage),
  },
  {
    path: 'confirmation/:reference',
    data: {
      titleKey: 'confirmation.meta.title',
      descriptionKey: 'confirmation.meta.description',
      noindex: true,
    },
    loadComponent: () =>
      import('./features/confirmation/confirmation').then((m) => m.ConfirmationPage),
  },

  /*
   * Hukuki sayfalar tek kaynak listeden uretilir; boylece "rota var ama alt
   * bilgide baglanti yok" (veya tersi) durumu yapisal olarak imkansizdir.
   */
  ...LEGAL_DOCUMENTS.map((document) => ({
    path: `legal/${document.slug}`,
    data: {
      document,
      titleKey: document.metaTitleKey,
      descriptionKey: `legal.${document.slug}.description`,
    },
    loadComponent: () => import('./features/legal/legal-page').then((m) => m.LegalPage),
  })),

  {
    path: '**',
    data: { titleKey: 'errors.notFound.title', noindex: true },
    loadComponent: () => import('./features/errors/not-found').then((m) => m.NotFoundPage),
  },
];

/**
 * Kok rota tablosu.
 *
 * Yalnizca UC durum vardir:
 *  1) `/`            -> dil pazarligi, kalici olarak `/de|/en|/tr`'ye yonlendirilir,
 *  2) `/{dil}/...`   -> kabuk + sayfa (dil desteklenmiyorsa BU rota eslesmez),
 *  3) diger her sey  -> dil pazarligi + yol korunarak yonlendirme.
 *
 * Yani dil on eksiz bir sayfa hicbir zaman servis edilmez; her icerigin tek
 * bir kanonik adresi olur.
 */
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [languageNegotiationGuard],
    children: [],
  },
  {
    path: ':lang',
    canMatch: [supportedLanguageMatch],
    canActivate: [languageRouteGuard],
    component: GuestShell,
    children: LANGUAGE_ROUTES,
  },
  {
    path: '**',
    canActivate: [languageNegotiationGuard],
    children: [],
  },
];
