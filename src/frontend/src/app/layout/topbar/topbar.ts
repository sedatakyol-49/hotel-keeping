import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { BrandMark } from '@hotelcore/shared';
import { HotelSwitcher } from '../hotel-switcher/hotel-switcher';
import { UserMenu } from '../user-menu/user-menu';

/**
 * Ust cubuk: **marka (en solda)**, mobil menu dugmesi, otel secici ve kullanici
 * menusu. 1px cetvel ile icerikten ayrilir; golge kullanilmaz.
 *
 * Kabuk bu cubugu akista **sabit** tutar (kaydirilmaz); bu yuzden burada ayrica
 * `sticky`/`fixed` konumlandirma yapilmaz — duzen sorumlulugu tek yerde kalir.
 *
 * DUZEN KARARLARI:
 * - **Marka her ekran boyutunda header'in en solundadir.** Header tam genislikte
 *   ve kenar cubugunun ustunde durdugu icin marka dogal olarak kenar cubugu
 *   sutununun ustune hizalanir (profesyonel panel deseni). Sol dolgu `lg`'de
 *   1rem'e iner: kenar cubugu kalemlerinin `px-4` dolgusuyla **birebir** ayni
 *   dikey hatta oturur.
 * - **Kenar cubugunu daraltma dugmesi burada degildir**; kenar cubugunun kendi
 *   ust blogundadir (bkz. `sidebar.html`) — denetim, denetledigi seyin yaninda.
 * - **Dil secici burada degildir**: dil Ayarlar ekranindan (ve giris ekranindan)
 *   secilir; ust cubuk operasyonel baglama (otel + kullanici) ayrilir.
 */
@Component({
  selector: 'hc-topbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, BrandMark, HotelSwitcher, UserMenu],
  template: `
    <header
      class="z-20 flex min-h-topbar shrink-0 items-center gap-3 border-b border-rule bg-paper px-3 sm:px-6 lg:pl-4"
    >
      <!--
        Marka blogu — header'in ilk ogesi, tum kirilimlarda gorunur.
        375px'te ust cubuk dar oldugu icin **ad metni yalnizca >= sm'de** cizilir;
        daha darda isaret tek basina markayi tasir. Bu yuzden erisilebilir ad
        isarette durur, gorunur metin ise onun tekrari sayilip gizlenir.
        Ad koda gomulmez: common.appName anahtarindan gelir.
      -->
      <div class="flex shrink-0 items-center gap-3 text-ink" data-testid="topbar-brand">
        <hc-brand-mark [size]="28" [label]="'common.appName' | translate" />
        <p class="hidden font-serif text-xl leading-none text-ink sm:block" aria-hidden="true">
          {{ 'common.appName' | translate }}
        </p>
      </div>

      @if (menuVisible()) {
        <button
          type="button"
          class="flex touch-target items-center justify-center border border-rule text-ink hover:bg-paper-sunken hover:border-rule-strong lg:hidden"
          [attr.aria-expanded]="menuOpen()"
          aria-controls="hc-mobile-drawer"
          [attr.aria-label]="(menuOpen() ? 'nav.closeMenu' : 'nav.openMenu') | translate"
          data-testid="menu-toggle"
          (click)="menuToggled.emit()"
        >
          @if (menuOpen()) {
            <!-- Kapat: iki capraz cetvel cizgisi. -->
            <svg
              class="hc-icon"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
              focusable="false"
              data-testid="icon-close"
            >
              <path d="M4 4l8 8M12 4l-8 8" />
            </svg>
          } @else {
            <!-- Menu: uc yatay cetvel cizgisi (defter satirlari). -->
            <svg
              class="hc-icon"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
              focusable="false"
              data-testid="icon-menu"
            >
              <path d="M2.5 4.5h11M2.5 8h11M2.5 11.5h11" />
            </svg>
          }
        </button>
      }

      <div class="ml-auto flex items-center gap-2 sm:gap-3">
        <hc-hotel-switcher />
        <hc-user-menu />
      </div>
    </header>
  `,
  styles: `
    /*
     * Ikon cizimi. Gorunurluk/yerlesim bilincli olarak Tailwind yardimcilarinda
     * birakildi: bilesen stilleri Angular tarafindan oznitelik seciciyle
     * yazildigi icin daha ozgul olur ve hidden / lg:... yardimcilarini ezerdi.
     * Burada yalnizca cizim ozellikleri var — cerceve ve dokunmatik hedef
     * sablondaki mevcut cetvel dilinden gelir.
     */
    .hc-icon {
      width: 1.125rem;
      height: 1.125rem;
      stroke: currentColor;
      stroke-width: 1.25;
      /* Kesin uclar ve sivri kose: defter dilinde yuvarlatma yok. */
      stroke-linecap: butt;
      stroke-linejoin: miter;
      shape-rendering: geometricPrecision;
    }
  `,
})
export class Topbar {
  readonly menuOpen = input(false);
  /** Hub ekraninda gezinme cekmecesi yoktur; menu dugmesi hic render edilmez. */
  readonly menuVisible = input(true);
  readonly menuToggled = output<void>();
}
