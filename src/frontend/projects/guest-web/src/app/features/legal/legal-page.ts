import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { PageIntro } from '../../shared/ui/page-intro/page-intro';
import type { LegalDocument } from './legal-documents';

/**
 * Hukuki belge sayfasi (Impressum / Datenschutz / AGB).
 *
 * Icerik bu turda YER TUTUCUDUR — ve bilincli olarak "Lorem ipsum" degildir:
 * her belge, doldurulmasi gereken **zorunlu bolum basliklarini** tasir
 * (ornegin kunyede saglayici, temsilci, iletisim, ticaret sicili, VAT no).
 * Boylece metin geldiginde yapinin neye benzemesi gerektigi bellidir ve
 * eksik bir bolum gozden kacmaz.
 *
 * Render modu: prerender. Bu sayfalar herkes icin ayni, nadiren degisir ve
 * her istekte sunucu isi harcamalari icin bir sebep yoktur.
 */
@Component({
  selector: 'hcg-legal-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageIntro],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'legal.eyebrow' | translate"
        [heading]="document().titleKey | translate"
      />

      <div class="mt-10 max-w-measure">
        @for (section of sections(); track section) {
          <section class="border-b border-rule py-6">
            <h2 class="font-serif text-xl" data-testid="legal-section">
              {{ section | translate }}
            </h2>
            <p class="mt-2 text-sm text-ink-muted">{{ 'legal.pending' | translate }}</p>
          </section>
        }
      </div>
    </div>
  `,
})
export class LegalPage {
  /** Rota `data` uzerinden baglanir (bkz. app.routes.ts). */
  readonly document = input.required<LegalDocument>();

  protected readonly sections = computed(() => {
    const slug = this.document().slug;
    return SECTIONS[slug].map((section) => `legal.${slug}.sections.${section}`);
  });
}

/** Her belgenin doldurulmasi gereken bolumleri (yasal asgari yapi). */
const SECTIONS: Readonly<Record<LegalDocument['slug'], readonly string[]>> = {
  // §5 DDG: saglayici, temsilci, iletisim, sicil, VAT, denetim makami, ODR.
  imprint: ['provider', 'contact', 'register', 'vat', 'supervision', 'disputeResolution'],
  // Art. 13/14 GDPR: sorumlu, veri koruma gorevlisi, amaclar, haklar, saklama.
  privacy: ['controller', 'officer', 'purposes', 'recipients', 'retention', 'rights'],
  // AGB: kapsam, rezervasyon, fiyat/odeme, iptal, konaklama kurallari, sorumluluk.
  terms: ['scope', 'booking', 'payment', 'cancellation', 'houseRules', 'liability'],
};
