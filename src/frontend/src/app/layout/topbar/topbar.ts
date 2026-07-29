import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { CurrentHotelService } from '../../core/services/current-hotel.service';
import { HotelSwitcher } from '../hotel-switcher/hotel-switcher';
import { LanguagePicker } from '../language-picker/language-picker';
import { UserMenu } from '../user-menu/user-menu';

/**
 * Ust cubuk: mobil menu dugmesi, otel secici, dil secici ve kullanici menusu.
 * 1px cetvel ile icerikten ayrilir; golge kullanilmaz.
 */
@Component({
  selector: 'hc-topbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, HotelSwitcher, LanguagePicker, UserMenu],
  template: `
    <header
      class="sticky top-0 z-20 flex min-h-topbar items-center gap-3 border-b border-rule bg-paper px-3 sm:px-6"
    >
      <button
        type="button"
        class="flex touch-target items-center justify-center border border-rule label-mono text-ink lg:hidden"
        [attr.aria-expanded]="menuOpen()"
        aria-controls="hc-mobile-drawer"
        [attr.aria-label]="(menuOpen() ? 'nav.closeMenu' : 'nav.openMenu') | translate"
        (click)="menuToggled.emit()"
      >
        <span aria-hidden="true">{{ menuOpen() ? '✕' : '≡' }}</span>
      </button>

      <p class="font-serif text-xl leading-none text-ink lg:hidden">
        {{ 'common.appName' | translate }}
      </p>

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
})
export class Topbar {
  protected readonly currentHotel = inject(CurrentHotelService);

  readonly menuOpen = input(false);
  readonly menuToggled = output<void>();
}
