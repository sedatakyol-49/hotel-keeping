import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';

import { NotificationStore } from '../../core/state/notification.store';
import { LanguagePicker } from '../language-picker/language-picker';
import { Sidebar } from '../sidebar/sidebar';
import { Topbar } from '../topbar/topbar';

/**
 * Uygulama kabugu: masaustunde sabit kenar cubugu, mobilde cekmece.
 * Kirilim noktalari 375 / 768 / 1440px icin dogrulanmistir.
 */
@Component({
  selector: 'hc-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, TranslatePipe, Sidebar, Topbar, LanguagePicker],
  templateUrl: './shell.html',
  host: {
    '(document:keydown.escape)': 'closeDrawer()',
  },
})
export class Shell {
  private readonly router = inject(Router);
  protected readonly notifications = inject(NotificationStore);

  protected readonly drawerOpen = signal(false);

  constructor() {
    // Rota degisiminde mobil cekmece kapanir.
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.closeDrawer());
  }

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }
}
