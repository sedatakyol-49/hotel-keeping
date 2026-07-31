import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import type { PublicImprint } from '../../core/api/public-models';
import { HotelStore } from '../../core/state/hotel.store';
import { ErrorPanel } from '../../shared/ui/error-panel/error-panel';
import { Notice } from '../../shared/ui/notice/notice';
import { PageIntro } from '../../shared/ui/page-intro/page-intro';
import type { LegalDocument } from './legal-documents';

/**
 * ===========================================================================
 * HUKUKI BELGE SAYFASI (Impressum / Datenschutz / AGB)
 * ===========================================================================
 *
 * ICERIK VERITABANINDAN GELIR, KODDAN DEGIL (`GET /public/hotels/{slug}/legal`).
 * §5 DDG kunye alanlari (13 alan) ve DSGVO Art. 13 metni musteri-degiskenidir;
 * hardcode edilirse her otel icin kod degistirmek gerekir ve bir gun yanlis
 * tuzel kisi gorunur.
 *
 * IMPRESSUM YAPISALDIR, METIN DEGILDIR: alanlar tek tek gelir ve burada bir
 * tanim listesine dokulur. Boylece bir alan (ornegin USt-IdNr.) eksikse
 * gorunur; serbest metin olsaydi eksiklik fark edilmezdi.
 *
 * AGB/Datenschutz `bodyHtml` olarak gelir ve **sunucuda sanitize edilmistir**
 * (sozlesme §2.3). Yine de Angular'in kendi sanitizer'i devrede birakilir
 * (`[innerHTML]`, `bypassSecurityTrust*` **kullanilmaz**): iki katman, tek
 * hatada acilmayan bir kapi demektir.
 *
 * RENDER MODU prerender. Su an derleme aninda API yok; sayfa iskeleti
 * onceden uretilir, icerik istemcide dolar. Uc canliya alindiginda prerender
 * isi (DevOps) icerigi derleme aninda gomer ve sayfa JS'siz de tam olur.
 */
@Component({
  selector: 'hcg-legal-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageIntro, Notice, ErrorPanel],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'legal.eyebrow' | translate"
        [heading]="document().titleKey | translate"
      />

      @if (document().slug === 'imprint') {
        @if (imprint(); as data) {
          <dl class="mt-10 max-w-measure border-t border-rule" data-testid="imprint-fields">
            @for (row of imprintRows(data); track row.key) {
              <div class="border-b border-rule py-3">
                <dt class="hc-label">{{ 'legal.imprint.fields.' + row.key | translate }}</dt>
                <dd class="mt-1 text-sm" [attr.data-testid]="'imprint-' + row.key">
                  {{ row.value }}
                </dd>
              </div>
            }
          </dl>

          <div class="mt-8 max-w-measure">
            <hcg-notice [heading]="'legal.imprint.sections.disputeResolution' | translate">
              <p data-testid="imprint-adr">
                {{
                  (data.disputeResolution.participatesInAdr
                    ? 'legal.adr.participating'
                    : 'legal.adr.notParticipating'
                  ) | translate
                }}
              </p>
              <p class="mt-2">
                <a [href]="data.disputeResolution.odrPlatformUrl" rel="noopener" target="_blank">
                  {{ data.disputeResolution.odrPlatformUrl }}
                </a>
              </p>
            </hcg-notice>
          </div>
        }
      } @else if (body(); as content) {
        <article class="hcg-prose mt-10 max-w-measure" data-testid="legal-body">
          <div [innerHTML]="content.bodyHtml"></div>
          <p class="numeric mt-8 text-xs text-ink-faint" data-testid="legal-version">
            {{ 'legal.version' | translate: { version: content.version } }}
          </p>
        </article>
      }

      @if (store.legalState().status === 'error' && store.legalState().error; as error) {
        <div class="mt-10 max-w-measure">
          <hcg-error-panel [error]="error" (recover)="retry()" />
        </div>
      } @else if (!hasContent()) {
        <!--
          Icerik gelmeden once: doldurulmasi gereken **zorunlu bolumler**
          gosterilir. "Lorem ipsum" degil; eksik bir bolum gozden kacmasin.
        -->
        <div class="mt-10 max-w-measure" data-testid="legal-pending">
          @for (section of sections(); track section) {
            <section class="border-b border-rule py-5">
              <h2 class="font-serif text-xl" data-testid="legal-section">
                {{ section | translate }}
              </h2>
              <p class="mt-2 text-sm text-ink-muted">{{ 'legal.pending' | translate }}</p>
            </section>
          }
        </div>
      }
    </div>
  `,
  styles: `
    /*
     * Sunucudan gelen HTML icin tipografi. Tailwind sinifi uygulanamaz (metin
     * bizim degil), bu yuzden ogeler dogrudan hedeflenir. Kural yine ayni:
     * yuvarlak kose yok, golge yok, 1px cetvel.
     */
    .hcg-prose :is(h2, h3) {
      font-family: var(--font-serif);
      font-size: 1.25rem;
      margin-top: 2rem;
    }
    .hcg-prose p,
    .hcg-prose li {
      font-size: 0.9375rem;
      color: var(--color-ink-muted);
      margin-top: 0.75rem;
    }
    .hcg-prose ul,
    .hcg-prose ol {
      margin-top: 0.75rem;
      padding-left: 1.25rem;
      list-style: disc;
    }
    .hcg-prose a {
      text-decoration: underline;
      text-underline-offset: 0.2em;
    }
    .hcg-prose table {
      width: 100%;
      margin-top: 1rem;
      border-top: 1px solid var(--color-rule);
    }
    .hcg-prose :is(th, td) {
      border-bottom: 1px solid var(--color-rule);
      padding: 0.5rem 0;
      text-align: left;
      font-size: 0.875rem;
    }
  `,
})
export class LegalPage {
  /** Rota `data` uzerinden baglanir (bkz. app.routes.ts). */
  readonly document = input.required<LegalDocument>();

  protected readonly store = inject(HotelStore);

  protected readonly imprint = computed(() => this.store.legal()?.imprint ?? null);

  protected readonly body = computed(() => {
    const slug = this.document().slug;
    if (slug === 'imprint') {
      return null;
    }
    return this.store.legal()?.documents.find((entry) => entry.key === slug) ?? null;
  });

  protected readonly hasContent = computed(() =>
    this.document().slug === 'imprint' ? this.imprint() !== null : this.body() !== null,
  );

  protected readonly sections = computed(() => {
    const slug = this.document().slug;
    return SECTIONS[slug].map((section) => `legal.${slug}.sections.${section}`);
  });

  constructor() {
    this.store.loadLegal();
  }

  protected retry(): void {
    this.store.retryLegal();
  }

  /** §5 DDG alanlari — bos olanlar gosterilmez, eksik olan gorunur kalir. */
  protected imprintRows(imprint: PublicImprint): readonly { key: string; value: string }[] {
    const rows: { key: string; value: string }[] = [
      { key: 'legalEntityName', value: imprint.legalEntityName },
      { key: 'legalForm', value: imprint.legalForm },
      { key: 'representedBy', value: imprint.representedBy },
      {
        key: 'address',
        value: `${imprint.addressLine}, ${imprint.postalCode} ${imprint.city}, ${imprint.country}`,
      },
      { key: 'phone', value: imprint.phone },
      { key: 'email', value: imprint.email },
      { key: 'registerCourt', value: imprint.registerCourt ?? '' },
      { key: 'registerNumber', value: imprint.registerNumber ?? '' },
      { key: 'vatId', value: imprint.vatId ?? '' },
      { key: 'supervisoryAuthority', value: imprint.supervisoryAuthority ?? '' },
    ];
    return rows.filter((row) => row.value.trim().length > 0);
  }
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
