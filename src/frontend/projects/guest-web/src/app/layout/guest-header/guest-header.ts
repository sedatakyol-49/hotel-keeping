import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { BrandMark, LanguageStore, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { CurrentUrlStore } from '../../core/routing/current-url.store';
import { languagePath, withLanguage } from '../../core/i18n/language-url';

/**
 * Misafir sitesi ust bilgisi — "gazete kunyesi" duzeni.
 *
 * DUZEN KARARI: iki satir, her ekran boyutunda AYNI DOM.
 * Satir 1 marka + dil + birincil eylem; satir 2 gezinme, aralarinda 1px cetvel.
 * Mobilde acilir cekmece (hamburger) YOK — bu sitede toplam dort gezinme
 * baglantisi var; onlari bir menunun arkasina saklamak, tiklamayi artirmaktan
 * baska bir sey yapmaz. Ayni DOM'un iki kirilimda calismasi ayrica SSR ciktisini
 * tekilleştirir: crawler ile kullanicinin gordugu markup ayni olur.
 *
 * Marka isareti `@hotelcore/shared` katmanindan gelir — panelle ayni monogram.
 * Ust bilgi bileseni ise PAYLASILMAZ: panelin yogun calisma cubugu ile buradaki
 * kunye farkli islerdir.
 */
@Component({
  selector: 'hcg-guest-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, BrandMark],
  template: `
    <header class="border-b border-rule bg-canvas" data-testid="guest-header">
      <div class="hcg-shell flex flex-wrap items-center gap-x-6 gap-y-3 py-4">
        <a
          [routerLink]="homePath()"
          class="mr-auto flex items-center gap-3 text-ink no-underline"
          data-testid="header-brand"
        >
          <hc-brand-mark [size]="32" [label]="'common.appName' | translate" />
          <span class="flex flex-col leading-none">
            <span class="font-serif text-2xl" aria-hidden="true">
              {{ 'common.appName' | translate }}
            </span>
            <span class="mt-1 eyebrow">{{ 'common.tagline' | translate }}</span>
          </span>
        </a>

        <!--
          Dil secici: baglanti (buton degil). Her dilin kendi ADRESI oldugu icin
          bunlar gercek gezinmelerdir; "hreflang" + "lang" nitelikleri hem
          ekran okuyucuya hem tarayiciya dogru sinyali verir.
        -->
        <nav [attr.aria-label]="'nav.languageNavigation' | translate" data-testid="language-switch">
          <ul class="flex items-center border border-rule">
            @for (language of languages; track language) {
              <li class="border-l border-rule first:border-l-0">
                <a
                  [routerLink]="urlFor(language)"
                  [attr.hreflang]="language"
                  [attr.lang]="language"
                  [attr.aria-current]="language === current() ? 'true' : null"
                  class="flex touch-target items-center justify-center px-3 label-mono no-underline"
                  [class.bg-ink]="language === current()"
                  [class.text-ink-inverse]="language === current()"
                  [class.text-ink-muted]="language !== current()"
                  [attr.data-testid]="'language-link-' + language"
                >
                  {{ language }}
                </a>
              </li>
            }
          </ul>
        </nav>

        <a [routerLink]="searchPath()" class="hcg-action" data-testid="header-cta">
          {{ 'nav.book' | translate }}
        </a>
      </div>

      <div class="border-t border-rule">
        <nav
          class="hcg-shell overflow-x-auto"
          [attr.aria-label]="'nav.mainNavigation' | translate"
          data-testid="main-nav"
        >
          <ul class="flex items-center gap-6">
            @for (item of navigation(); track item.path) {
              <li>
                <!--
                  Aktif isaret CSS sinifiyla degil "aria-current" ile verilir;
                  gorunum ".hcg-nav-link[aria-current=page]" kuralindan gelir.
                  Boylece gorsel ve erisilebilir durum ayrilamaz.
                -->
                <a
                  [routerLink]="item.path"
                  [attr.aria-current]="item.active ? 'page' : null"
                  class="hcg-nav-link whitespace-nowrap text-sm no-underline"
                  [attr.data-testid]="'nav-' + item.testId"
                >
                  {{ item.labelKey | translate }}
                </a>
              </li>
            }
          </ul>
        </nav>
      </div>
    </header>
  `,
})
export class GuestHeader {
  private readonly language = inject(LanguageStore);
  private readonly currentUrl = inject(CurrentUrlStore);

  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly current = this.language.current;

  protected readonly homePath = computed(() => languagePath(this.current()));
  protected readonly searchPath = computed(() => languagePath(this.current(), 'search'));

  /*
   * Aktif durum `RouterLinkActive` yerine URL'den TURETILIR. Sebep: aktiflik
   * bilgisi zaten `CurrentUrlStore` signal'inde var; direktife birakildiginda
   * deger ikinci bir degisiklik denetimi turunda yerlesir ve OnPush bir
   * bilesende `aria-current` bir tur geriden gelir.
   */
  protected readonly navigation = computed(() => {
    const language = this.current();
    const url = stripSuffix(this.currentUrl.url());
    const home = languagePath(language);
    const search = languagePath(language, 'search');

    return [
      { testId: 'home', path: home, labelKey: 'nav.home', active: url === home },
      {
        testId: 'rooms',
        path: search,
        labelKey: 'nav.rooms',
        active: url === search || url.startsWith(`${search}/`),
      },
    ];
  });

  /** Ayni sayfanin baska dildeki adresi (kullanici yerini kaybetmez). */
  protected urlFor(language: (typeof SUPPORTED_LANGUAGES)[number]): string {
    return withLanguage(this.currentUrl.url(), language);
  }
}

/** Sorgu/parca kismi aktiflik karsilastirmasina girmez. */
function stripSuffix(url: string): string {
  const found = [url.indexOf('?'), url.indexOf('#')].filter((index) => index !== -1);
  return found.length > 0 ? url.slice(0, Math.min(...found)) : url;
}
