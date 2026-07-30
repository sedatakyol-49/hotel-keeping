import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { GuestFooter } from '../guest-footer/guest-footer';
import { GuestHeader } from '../guest-header/guest-header';

/**
 * Misafir sitesi kabugu — dil on ekli TUM rotalarin ortak cercevesi.
 *
 * Semantik iskelet burada bir kez kurulur:
 *   skip link -> <header> -> <main id="content"> -> <footer>
 * `main` bir landmark'tir ve `tabindex="-1"` tasir; atlama baglantisi odagi
 * gercekten oraya tasiyabilsin diye (yalnizca `#content` hedefi Safari/Firefox'ta
 * odagi tasimaz, sadece kaydirir).
 *
 * Alt bilgi kabukta oldugu icin hukuki baglantilar her sayfada garanti altindadir.
 */
@Component({
  selector: 'hcg-guest-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, TranslatePipe, GuestHeader, GuestFooter],
  template: `
    <a class="hc-skip-link" href="#content">{{ 'a11y.skipToContent' | translate }}</a>

    <div class="flex min-h-dvh flex-col">
      <hcg-guest-header />

      <main id="content" tabindex="-1" class="flex-1 outline-none" data-testid="guest-main">
        <router-outlet />
      </main>

      <hcg-guest-footer />
    </div>
  `,
})
export class GuestShell {}
