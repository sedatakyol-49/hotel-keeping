import type { Routes } from '@angular/router';

import { HIDE_SIDEBAR } from '../../layout/chrome';

/**
 * `/dashboard` = hub (launcher). Kenar cubugu burada gizlenir: moduller kart
 * izgarasi olarak sunuldugu icin ikinci bir gezinme sutunu gereksizdir.
 */
export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    data: { titleKey: 'dashboard.title', [HIDE_SIDEBAR]: true },
    loadComponent: () => import('./hub').then((m) => m.HubPage),
  },
];
