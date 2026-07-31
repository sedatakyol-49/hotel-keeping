import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore } from '@hotelcore/shared';

import { ConsentStore } from '../../../core/consent/consent.store';
import { languagePath } from '../../../core/i18n/language-url';
import { BrowserStorage } from '../../../core/storage/browser-storage';

/**
 * ===========================================================================
 * §25 TDDDG — ONAY BANDI
 * ===========================================================================
 *
 * ARAYUZ KURALLARI (yalnizca "iyi uygulama" degil, denetim konusu):
 *  1) **Iki dugme, ayni agirlik.** "Reddet" ile "Kabul et" ayni olcude, ayni
 *     renkte, ayni seviyede ve ayni tiklama sayisindadir. Kabul dugmesini dolu
 *     murekkeple, reddi gri bir baglanti olarak cizmek — yaygin olsa da —
 *     onayi "serbestce verilmis" olmaktan cikarir (DSGVO Art. 4 Nr. 11).
 *  2) **On isaretli kutu yok.** Zaten kutu yok: karar iki acik eylemden biri.
 *  3) **Zorunlu depolama ayri anlatilir.** Rezervasyonun yurumesi icin gereken
 *     `holdToken` ve dil tercihi §25 Abs. 2 Nr. 2 istisnasi kapsamindadir ve
 *     **onaya tabi degildir**; bant bunu gizlemek yerine acikca soyler.
 *  4) **Karar geri alinabilir.** Alt bilgideki "Cerez ayarlari" bandi acar.
 *
 * Bant yalnizca **tarayicida** ve karar verilmemisken cizilir. SSR ciktisinda
 * bulunmaz: sunucu kullanicinin kararini bilemez (public uclar cerez koymaz),
 * ve statik olarak prerender edilmis bir bant her ziyaretciye "kararsiz" gibi
 * gorunurdu.
 *
 * Konumlandirma `sticky` degil `fixed`: sayfa akisini bozmaz, ama ekranin
 * altinda kalir. Icerigin altina `padding` eklenmez — bant kapanicidir ve
 * kalici bir kayip degildir.
 */
@Component({
  selector: 'hcg-cookie-consent',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe],
  template: `
    @if (visible()) {
      <section
        class="fixed inset-x-0 bottom-0 z-50 border-t border-ink bg-paper-raised"
        [attr.aria-label]="'consent.label' | translate"
        data-testid="cookie-consent"
      >
        <div class="hcg-shell flex flex-col gap-4 py-5 lg:flex-row lg:items-center lg:gap-8">
          <div class="max-w-measure">
            <p class="eyebrow">{{ 'consent.label' | translate }}</p>
            <h2 class="mt-1 font-serif text-xl">{{ 'consent.title' | translate }}</h2>
            <p class="mt-2 text-sm text-ink-muted" data-testid="consent-body">
              {{ 'consent.body' | translate }}
            </p>
            <p class="mt-2 text-xs text-ink-faint" data-testid="consent-essential">
              {{ 'consent.essential' | translate }}
            </p>
            <a
              [routerLink]="privacyPath()"
              class="mt-2 inline-block text-xs underline underline-offset-4"
              data-testid="consent-privacy-link"
            >
              {{ 'consent.privacyLink' | translate }}
            </a>
          </div>

          <!--
            Iki dugme AYNI sinifla cizilir. Sira "reddet" -> "kabul et";
            reddin once gelmesi, alisilmis "kabul et sagda ve vurgulu" kalibini
            kirar ve karari gercekten esitler.
          -->
          <div class="flex flex-col gap-3 sm:flex-row lg:ml-auto">
            <button
              type="button"
              class="hcg-action hcg-action--quiet"
              data-testid="consent-decline"
              (click)="decline()"
            >
              {{ 'consent.decline' | translate }}
            </button>
            <button
              type="button"
              class="hcg-action hcg-action--quiet"
              data-testid="consent-accept"
              (click)="accept()"
            >
              {{ 'consent.accept' | translate }}
            </button>
          </div>
        </div>
      </section>
    }
  `,
})
export class CookieConsent {
  private readonly consent = inject(ConsentStore);
  private readonly storage = inject(BrowserStorage);
  private readonly language = inject(LanguageStore);

  protected readonly visible = computed(
    () => this.storage.browser && this.consent.bannerVisible(),
  );

  protected readonly privacyPath = computed(() =>
    languagePath(this.language.current(), 'legal', 'privacy'),
  );

  protected accept(): void {
    this.consent.accept(this.storage.local);
  }

  protected decline(): void {
    this.consent.decline(this.storage.local);
  }
}
