import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';

import { NotificationStore } from '../../core/state/notification.store';
import { shouldHideSidebar } from '../chrome';
import { Sidebar } from '../sidebar/sidebar';
import { SidebarState } from '../sidebar-state';
import { Topbar } from '../topbar/topbar';

/**
 * Uygulama kabugu: masaustunde sabit kenar cubugu, mobilde cekmece.
 * Kirilim noktalari 375 / 768 / 1440px icin dogrulanmistir.
 *
 * Hub rotasi (`/dashboard`) `HIDE_SIDEBAR` bayragini tasidigi icin orada kenar
 * cubugu ve mobil menu dugmesi hic render edilmez; bir module girildiginde
 * kabuk normal duzenine doner. Topbar (marka, otel secici, kullanici menusu)
 * her iki durumda da yerinde kalir.
 *
 * Kenar cubugunu daraltma dugmesi cubugun kendi ust blogundadir; bu yuzden hub
 * ekraninda dogal olarak gorunmez — orada daraltilacak bir cubuk zaten yoktur.
 * Kabuk daralma **durumunu** yine de okur: sutun genisligini o belirler.
 */
@Component({
  selector: 'hc-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, TranslatePipe, Sidebar, Topbar],
  templateUrl: './shell.html',
  host: {
    '(document:keydown.escape)': 'closeDrawer()',
  },
})
export class Shell {
  private readonly router = inject(Router);
  private readonly sidebarState = inject(SidebarState);
  protected readonly notifications = inject(NotificationStore);

  protected readonly drawerOpen = signal(false);
  /** Kenar cubugu ve mobil menu dugmesi gorunur mu (rota `data` bayragindan). */
  protected readonly navigationVisible = signal(true);
  /** Masaustunde kenar cubugu rail moduna alinmis mi (kullanici tercihi, kalici). */
  protected readonly sidebarCollapsed = this.sidebarState.collapsed;

  constructor() {
    this.syncNavigationVisibility();

    // Rota degisiminde mobil cekmece kapanir ve kabuk duzeni yeniden karara baglanir.
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => {
        this.closeDrawer();
        this.syncNavigationVisibility();
      });
  }

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  private syncNavigationVisibility(): void {
    const hidden = shouldHideSidebar(this.router.routerState.snapshot.root);
    this.navigationVisible.set(!hidden);
    if (hidden) {
      this.drawerOpen.set(false);
    }
  }
}
