import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Marka isareti — "HS" monogrami, **satir ici SVG** (harici istek yok, CSP guvenli).
 *
 * TASARIM KARARI (neden geometrik, neden bu agirlik):
 * - Harfler `--font-serif` (Instrument Serif) ile **yazilmaz**, cizilir. Instrument
 *   Serif yuksek kontrastli bir display kesimidir; ince cizgileri 24px altinda
 *   kaybolur, dolayisiyla favicon/manifest olcusunde okunmaz. Cizilmis geometrik
 *   monogram ise dikey/yatay kenarlari tam piksel izgarasina oturur.
 * - Voice olarak da dogru olan bu: sistemde **kayit tutan** her sey mono
 *   (IBM Plex Mono). Monogram bu rasyonel sese yaslanir, editoryal baslik sesine
 *   degil — baslik serifi zaten markanin **yazi** karsiligidir.
 * - Govde kalinligi 3/32 birim (cap yuksekliginin ~%15'i). 16px'te 1.5px, 32px'te
 *   3px gover; daha ince bir govde favicon'da grilesirdi.
 * - Cerceve `vector-effect="non-scaling-stroke"` ile **her olcekte tam 1px** kalir;
 *   bu, tasarim sisteminin "1px cetvel" dilinin isarete tasinmis halidir. Yuvarlak
 *   kose, golge, gradyan yoktur.
 * - Tek renk (`currentColor` = murekkep) + tek aksan: **bakir** taban cizgisi.
 *   Bakir secildi cunku lacivert etkilesim (birincil eylem/odak) rengidir;
 *   marka aksani onunla yarismamalidir.
 *
 * MARKA ADI KODA GOMULMEZ: bu bilesen yalnizca **isareti** cizer. Gorunen ad
 * `common.appName` i18n anahtarindan (ileride Head Office ayarlarindan) gelir ve
 * cagiran sablonda isaretin yaninda durur. Bu yuzden varsayilan davranis
 * "susleme"dir (`aria-hidden`): ad zaten metin olarak okunur. Isaretin tek basina
 * durdugu yerlerde `label` verilir ve SVG `role="img"` + `aria-label` tasir.
 */
@Component({
  selector: 'hc-brand-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 32 32"
      fill="none"
      shape-rendering="geometricPrecision"
      focusable="false"
      [attr.role]="label() ? 'img' : null"
      [attr.aria-label]="label() || null"
      [attr.aria-hidden]="label() ? null : 'true'"
      data-testid="brand-mark"
    >
      <!-- Defter plakasi: olcekten bagimsiz 1px cetvel cercevesi. -->
      <rect
        x="0.5"
        y="0.5"
        width="31"
        height="31"
        stroke="currentColor"
        stroke-width="1"
        vector-effect="non-scaling-stroke"
      />
      <!-- H: iki dikey govde + orta kusak (cap 4.5 -> 24.5, govde 3 birim). -->
      <path d="M3 4.5h3v20H3zM11.5 4.5h3v20h-3zM3 13h11.5v3H3z" fill="currentColor" />
      <!--
        S: H ile ayni 3 birim govde kalinliginda tek cizgi (monoline). Iki daire
        yayi (r=4.25) tam orta hizada (y=14.5) birlestigi icin ek/kirilma gorunmez.
      -->
      <path
        d="M26.73 7.81A4.25 4.25 0 1 0 23.25 14.5a4.25 4.25 0 1 1-3.48 6.69"
        stroke="currentColor"
        stroke-width="3"
      />
      <!-- Tek aksan: bakir taban cizgisi (defter satiri). -->
      <rect x="3" y="26.5" width="26" height="1.5" fill="var(--color-copper, #a9662f)" />
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      line-height: 0;
    }
  `,
})
export class BrandMark {
  /** Kenar uzunlugu (px). Kare isaret; 16 ile 96 arasinda dogrulandi. */
  readonly size = input(32);

  /**
   * Erisilebilir ad. **Bos birakilirsa** isaret susleme sayilir (`aria-hidden`) —
   * yanindaki gorunur marka adi zaten okunur. Isaretin tek basina durdugu
   * yerlerde cagiran taraf cevrilmis adi (`common.appName`) buraya verir.
   */
  readonly label = input('');
}
