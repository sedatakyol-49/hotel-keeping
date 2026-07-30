import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { BrandMark, LanguageStore, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { CurrentUrlStore } from '../../core/routing/current-url.store';
import { LEGAL_DOCUMENTS } from '../../features/legal/legal-documents';
import { languagePath, withLanguage } from '../../core/i18n/language-url';

/**
 * Misafir sitesi alt bilgisi.
 *
 * HUKUKI ZORUNLULUK — bu bilesen sussuz degildir:
 * §5 DDG (eski TMG) uyarinca Almanya'da is amaçli her telemedya sunumunda
 * kunye (Impressum) "kolay taninabilir, dogrudan ulasilabilir ve surekli
 * erisilebilir" olmak zorundadir; yerlesik yargi pratiginde bu "her sayfadan
 * en fazla iki tik" olarak okunur. Ayni sekilde gizlilik bildirimi (Art. 12-14
 * GDPR) ve AGB de her sayfadan ulasilabilir olmalidir.
 *
 * Bu yuzden alt bilgi **kabuk** bileseninde durur, sayfalara birakilmaz: bir
 * sayfanin bu baglantilari koymayi unutmasi mumkun degildir. `legal-documents.ts`
 * tek kaynak listedir; rota tablosu ve bu liste ayni diziden beslenir, biri
 * eklenip digeri unutulamaz.
 */
@Component({
  selector: 'hcg-guest-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, BrandMark],
  template: `
    <footer class="border-t border-rule bg-canvas-deep" data-testid="guest-footer">
      <div class="hcg-shell grid gap-x-8 gap-y-10 py-12 md:grid-cols-3">
        <div class="max-w-measure">
          <div class="flex items-center gap-3 text-ink">
            <hc-brand-mark [size]="28" [label]="'common.appName' | translate" />
            <span class="font-serif text-xl" aria-hidden="true">
              {{ 'common.appName' | translate }}
            </span>
          </div>
          <p class="mt-4 text-sm text-ink-muted">{{ 'footer.about' | translate }}</p>
        </div>

        <nav [attr.aria-label]="'footer.legalNavigation' | translate">
          <h2 class="eyebrow">{{ 'footer.legal' | translate }}</h2>
          <ul class="mt-4 border-t border-rule">
            @for (document of legalLinks(); track document.slug) {
              <li class="border-b border-rule">
                <a
                  [routerLink]="document.path"
                  class="flex touch-target items-center text-sm text-ink no-underline hover:text-copper"
                  [attr.data-testid]="'legal-link-' + document.slug"
                >
                  {{ document.labelKey | translate }}
                </a>
              </li>
            }
          </ul>
        </nav>

        <nav [attr.aria-label]="'nav.languageNavigation' | translate">
          <h2 class="eyebrow">{{ 'footer.language' | translate }}</h2>
          <ul class="mt-4 border-t border-rule">
            @for (language of languages; track language) {
              <li class="border-b border-rule">
                <a
                  [routerLink]="urlFor(language)"
                  [attr.hreflang]="language"
                  [attr.lang]="language"
                  [attr.aria-current]="language === current() ? 'true' : null"
                  class="flex touch-target items-center gap-3 text-sm text-ink no-underline hover:text-copper"
                  [attr.data-testid]="'footer-language-' + language"
                >
                  <span class="label-mono text-ink-faint">{{ language }}</span>
                  <span>{{ 'language.' + language | translate }}</span>
                </a>
              </li>
            }
          </ul>
        </nav>
      </div>

      <div class="border-t border-rule">
        <div
          class="hcg-shell flex flex-col gap-2 py-5 text-xs text-ink-muted sm:flex-row sm:items-center sm:justify-between"
        >
          <p class="numeric">{{ 'footer.copyright' | translate: { year: year } }}</p>
          <p>{{ 'footer.priceNote' | translate }}</p>
        </div>
      </div>
    </footer>
  `,
})
export class GuestFooter {
  private readonly language = inject(LanguageStore);
  private readonly currentUrl = inject(CurrentUrlStore);

  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly current = this.language.current;
  protected readonly year = new Date().getFullYear();

  protected readonly legalLinks = computed(() =>
    LEGAL_DOCUMENTS.map((document) => ({
      ...document,
      path: languagePath(this.current(), 'legal', document.slug),
    })),
  );

  protected urlFor(language: (typeof SUPPORTED_LANGUAGES)[number]): string {
    return withLanguage(this.currentUrl.url(), language);
  }
}
