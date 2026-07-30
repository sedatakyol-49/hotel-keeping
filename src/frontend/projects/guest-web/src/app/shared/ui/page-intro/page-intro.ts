import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Sayfa girisi — eyebrow + H1 + lede.
 *
 * Her sayfada tek bir `<h1>` olmasini garanti eder (SEO + ekran okuyucu) ve
 * editoryal olcegi tek yerde tutar. Panelin `PageHeader` bileseniyle akraba
 * ama ayni degil: orada baslik 1.25rem ve saginda eylem dugmeleri vardir;
 * burada baslik `text-headline` ve altinda okuma genisliginde bir giris metni.
 */
@Component({
  selector: 'hcg-page-intro',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="border-b border-rule pb-8">
      @if (eyebrow(); as text) {
        <p class="eyebrow" data-testid="page-eyebrow">{{ text }}</p>
      }
      <h1 class="mt-3 text-headline" data-testid="page-title">{{ heading() }}</h1>
      @if (lede(); as text) {
        <p class="mt-4 max-w-measure text-lede text-ink-muted" data-testid="page-lede">
          {{ text }}
        </p>
      }
    </div>
  `,
})
export class PageIntro {
  readonly heading = input.required<string>();
  readonly eyebrow = input('');
  readonly lede = input('');
}
