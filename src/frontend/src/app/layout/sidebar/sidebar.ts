import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthStore } from '../../core/state/auth.store';
import { NAV_SECTIONS, filterNavSections, type NavSection } from '../navigation';

/**
 * Ana gezinme listesi. Masaustunde sabit sutun, mobilde cekmece icerigi olarak
 * ayni bilesen kullanilir (tek kaynak, iki yerlesim).
 *
 * Modul listesi `layout/navigation.ts` dizisinden gelir — hub kart izgarasi da
 * ayni diziyi okur, boylece yeni modul iki yerde tanimlanmaz.
 */
@Component({
  selector: 'hc-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <nav class="flex h-full flex-col" [attr.aria-label]="'nav.mainNavigation' | translate">
      <div class="border-b border-rule px-4 py-4">
        <p class="font-serif text-2xl leading-none text-ink">{{ 'common.appName' | translate }}</p>
        <p class="mt-1 eyebrow">{{ 'common.tagline' | translate }}</p>
      </div>

      <div class="flex-1 overflow-y-auto py-2">
        @for (section of sections(); track section.labelKey) {
          <div class="px-4 pt-4 pb-1">
            <p class="eyebrow">{{ section.labelKey | translate }}</p>
          </div>
          <ul>
            @for (item of section.items; track item.path) {
              <li>
                <a
                  [routerLink]="item.path"
                  routerLinkActive="border-l-copper bg-paper-sunken text-ink"
                  [routerLinkActiveOptions]="{ exact: false }"
                  ariaCurrentWhenActive="page"
                  class="flex min-h-touch items-center border-l-2 border-l-transparent px-4 text-sm text-ink-muted hover:bg-paper-sunken hover:text-ink"
                  (click)="navigated.emit()"
                >
                  {{ item.labelKey | translate }}
                </a>
              </li>
            }
          </ul>
        }
      </div>

      <div class="border-t border-rule px-4 py-3">
        <p class="eyebrow">{{ 'common.version' | translate }} 0.1.0</p>
      </div>
    </nav>
  `,
})
export class Sidebar {
  private readonly authStore = inject(AuthStore);

  /** Mobil cekmecede baglantiya tiklaninca kapatmak icin. */
  readonly navigated = output<void>();

  /** Kullanicinin izinlerine gore filtrelenmis bolumler. */
  protected readonly sections = computed<readonly NavSection[]>(() =>
    filterNavSections(NAV_SECTIONS, (item) =>
      this.authStore.matchesPermissions(item.permissions),
    ),
  );
}
