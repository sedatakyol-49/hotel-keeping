import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { CurrentHotelService } from '../../core/services/current-hotel.service';
import { BrandMark } from '../../shared/ui/brand-mark/brand-mark';
import { HotelSwitcher } from '../hotel-switcher/hotel-switcher';
import { LanguagePicker } from '../language-picker/language-picker';
import { UserMenu } from '../user-menu/user-menu';

/**
 * Ust cubuk: mobil menu dugmesi, masaustu kenar cubugu daraltma dugmesi, otel
 * secici, dil secici ve kullanici menusu. 1px cetvel ile icerikten ayrilir;
 * golge kullanilmaz.
 *
 * Kabuk bu cubugu akista **sabit** tutar (kaydirilmaz); bu yuzden burada ayrica
 * `sticky`/`fixed` konumlandirma yapilmaz — duzen sorumlulugu tek yerde kalir.
 */
@Component({
  selector: 'hc-topbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, BrandMark, HotelSwitcher, LanguagePicker, UserMenu],
  template: `
    <header
      class="z-20 flex min-h-topbar shrink-0 items-center gap-3 border-b border-rule bg-paper px-3 sm:px-6"
    >
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

      @if (sidebarToggleVisible()) {
        <!-- Daraltma yalnizca masaustunde anlamli: mobilde gezinme cekmecededir. -->
        <button
          type="button"
          class="hidden touch-target items-center justify-center border border-rule text-ink hover:bg-paper-sunken hover:border-rule-strong lg:flex"
          [attr.aria-pressed]="sidebarCollapsed()"
          [attr.aria-label]="
            (sidebarCollapsed() ? 'nav.expandSidebar' : 'nav.collapseSidebar') | translate
          "
          [attr.title]="
            (sidebarCollapsed() ? 'nav.expandSidebar' : 'nav.collapseSidebar') | translate
          "
          data-testid="sidebar-toggle"
          (click)="sidebarToggled.emit()"
        >
          <!--
            Panel gostergesi: solda kenar cubugunun kenari (dikey cetvel), yaninda
            hareket yonunu gosteren sivri chevron. Daraltilmisken ok disa (genislet),
            genisken ice (daralt) bakar.
          -->
          <svg
            class="hc-icon"
            viewBox="0 0 16 16"
            fill="none"
            aria-hidden="true"
            focusable="false"
            [attr.data-testid]="sidebarCollapsed() ? 'icon-panel-expand' : 'icon-panel-collapse'"
          >
            <path d="M4 2.5v11" />
            @if (sidebarCollapsed()) {
              <path d="M8 4.5L12 8l-4 3.5" />
            } @else {
              <path d="M12 4.5L8 8l4 3.5" />
            }
          </svg>
        </button>
      }

      <!--
        Mobilde kenar cubugu yok: marka blogu burada durur. 375px'te ust cubuk
        zaten dar oldugu icin **ad metni yalnizca >= sm'de** gorunur; daha darda
        isaret tek basina markayi tasir. Bu yuzden erisilebilir ad isarette
        durur, gorunur metin ise onun tekrari sayilip gizlenir.
      -->
      <div class="flex items-center gap-2 text-ink lg:hidden">
        <hc-brand-mark [size]="26" [label]="'common.appName' | translate" />
        <p class="hidden font-serif text-xl leading-none text-ink sm:block" aria-hidden="true">
          {{ 'common.appName' | translate }}
        </p>
      </div>

      <div class="ml-auto flex items-center gap-2 sm:gap-3">
        @if (currentHotel.isConsolidated()) {
          <p class="hidden eyebrow md:block">{{ 'hotel.consolidated' | translate }}</p>
        }
        <hc-hotel-switcher />
        <div class="hidden sm:block">
          <hc-language-picker />
        </div>
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
  protected readonly currentHotel = inject(CurrentHotelService);

  readonly menuOpen = input(false);
  /** Hub ekraninda gezinme cekmecesi yoktur; menu dugmesi hic render edilmez. */
  readonly menuVisible = input(true);
  readonly menuToggled = output<void>();

  /** Kenar cubugu daraltilmis mi (dugmenin basili durumu ve etiketi icin). */
  readonly sidebarCollapsed = input(false);
  /** Hub ekraninda kenar cubugu yoktur; daraltma dugmesi de gosterilmez. */
  readonly sidebarToggleVisible = input(true);
  readonly sidebarToggled = output<void>();
}
