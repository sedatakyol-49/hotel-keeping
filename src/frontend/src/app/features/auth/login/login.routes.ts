import type { Routes } from '@angular/router';

import { guestGuard } from '../../../core/guards/auth.guard';

export const LOGIN_ROUTES: Routes = [
  {
    path: '',
    canActivate: [guestGuard],
    data: { titleKey: 'auth.title' },
    loadComponent: () => import('./login').then((m) => m.LoginPage),
  },
];
