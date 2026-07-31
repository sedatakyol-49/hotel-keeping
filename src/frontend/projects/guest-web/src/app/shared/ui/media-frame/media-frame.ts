import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';

/**
 * Medya cercevesi — gorsel alani icin **olcusu dogru** yer tutucu.
 *
 * NEDEN BOYLE BIR BILESEN VAR (CLS disiplini):
 * Gorsel alanlari sonra veritabanindan gelecek. Yer tutucu ile gercek gorsel
 * ayni kutuyu kullanmazsa, fotograf yuklendiginde sayfa zipliyor demektir
 * (Cumulative Layout Shift). Bu yuzden kutu **her zaman** `width`/`height`
 * oranindan hesaplanan `aspect-ratio` ile onceden ayrilir; `src` geldiginde
 * ayni kutunun icine `<img>` girer, hicbir sey kaymaz.
 *
 * Yukleme disiplini `priority` ile tek yerde toplanir:
 *   priority=true  -> loading="eager"  + fetchpriority="high"  (LCP adayi)
 *   priority=false -> loading="lazy"   + fetchpriority="auto"  + decoding="async"
 * Sayfa basina en fazla BIR priority gorsel olmalidir.
 *
 * CIZIM: gradyan/ikon/emoji yok. Bos kutu, 1px cetvel cercevesi ve teknik
 * cizimden odunc alinmis capraz iki cizgi ("burada gorsel var") ile isaretlenir;
 * kose altinda mono etiket olcuyu okunur kilar.
 *
 * KIRIK GORSEL YOKTUR: `src` verilmis ama yuklenememisse (404/500 — bu fazda
 * seed yollari `/assets/demo/...` gercek dosyalara isaret etmiyor) tarayicinin
 * kirik gorsel simgesi ve alt metni yerine AYNI yer tutucu cizilir. Yer tutucu
 * kasitli bir tasarim ogesidir; kirik simge bir hatadir ve sayfayi terk
 * edilmis gosterir.
 */
@Component({
  selector: 'hcg-media-frame',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <figure class="m-0" [style.aspect-ratio]="ratio()">
      @if (source(); as url) {
        <img
          class="block h-full w-full object-cover"
          [src]="url"
          [attr.width]="width()"
          [attr.height]="height()"
          [alt]="alt()"
          [attr.loading]="priority() ? 'eager' : 'lazy'"
          [attr.fetchpriority]="priority() ? 'high' : 'auto'"
          decoding="async"
          data-testid="media-image"
          (error)="onError()"
        />
      } @else {
        <div
          class="relative h-full w-full border border-rule bg-media"
          role="img"
          [attr.aria-label]="alt()"
          data-testid="media-placeholder"
        >
          <svg
            class="absolute inset-0 h-full w-full"
            viewBox="0 0 100 100"
            preserveAspectRatio="none"
            aria-hidden="true"
            focusable="false"
          >
            <path
              d="M0 0L100 100M100 0L0 100"
              stroke="var(--color-media-line)"
              stroke-width="1"
              vector-effect="non-scaling-stroke"
              shape-rendering="geometricPrecision"
            />
          </svg>
          @if (caption(); as text) {
            <span class="absolute bottom-0 left-0 eyebrow bg-canvas px-2 py-1 text-ink-muted">
              {{ text }}
            </span>
          }
        </div>
      }
    </figure>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class MediaFrame {
  /** Kaynak olcu — oran buradan hesaplanir, hicbir sayfa kendi oranini uydurmaz. */
  readonly width = input.required<number>();
  readonly height = input.required<number>();
  /** Gorsel geldiginde doldurulur; bos oldugu surece yer tutucu cizilir. */
  readonly src = input<string | null>(null);
  /** Erisilebilir aciklama — yer tutucuda da gereklidir (bos alt = "susleme"). */
  readonly alt = input.required<string>();
  /** Kose etiketi (ornegin oda tipi adi). */
  readonly caption = input('');
  /** Sayfanin LCP adayi mi? Sayfa basina en fazla bir kez `true`. */
  readonly priority = input(false);

  protected readonly ratio = computed(() => `${this.width()} / ${this.height()}`);

  /** Yuklenemeyen kaynak; yer tutucuya duseriz. */
  private readonly failed = signal(false);

  /** Gosterilecek kaynak: yuklenemediyse `null` -> yer tutucu. */
  protected readonly source = computed(() => (this.failed() ? null : this.src()));

  constructor() {
    // Kaynak degisince yeniden denenir (eski hata yeni gorseli engellemesin).
    effect(() => {
      this.src();
      this.failed.set(false);
    });
  }

  protected onError(): void {
    this.failed.set(true);
  }
}
