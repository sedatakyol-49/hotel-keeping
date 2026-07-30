import type { Routes } from '@angular/router';

/**
 * Ayarlar rotasi **oturum acmis her kullaniciya** aciktir (ust seviyedeki
 * `authGuard` disinda ek koruma yoktur).
 *
 * NEDEN `permissionGuard(Settings.Manage)` KALDIRILDI: bu ekranin ilk karti
 * **arayuz dili** — kisisel bir tercih, yonetim ayari degil. `Settings.Manage`
 * otel kunyesi, vergi numarasi ve Head Office yapilandirmasi gibi **kurumu**
 * etkileyen alanlari korur; kullanicinin kendi dili onlarla ayni kapiya
 * konamaz. Rota kapali kalsaydi, dil secicisi ust cubuktan alindigi icin
 * `Settings.Manage` izni olmayan bir kullanici (Resepsiyon, Housekeeping)
 * oturum icinde dilini hic degistiremezdi.
 *
 * Yetki denetimi **sayfa icine** indi (bkz. `settings.ts` -> `canManageSettings`).
 * Bu istemci tarafi ayrim bir **guvenlik siniri degildir**, yalnizca gurultu
 * azaltmadir: `GET /hotels`, `PUT /hotels/{id}/settings` ve
 * `GET|PUT /head-office/settings` uclarinin yetki denetimi sunucuda oldugu gibi
 * durur.
 */
export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    data: { titleKey: 'settings.title' },
    loadComponent: () => import('./settings').then((m) => m.SettingsPage),
  },
];
