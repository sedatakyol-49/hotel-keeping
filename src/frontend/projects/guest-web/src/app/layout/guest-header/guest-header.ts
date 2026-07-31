import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { BrandMark, LanguageStore, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { CurrentUrlStore } from '../../core/routing/current-url.store';
import { languagePath, withLanguage } from '../../core/i18n/language-url';

/**
 * ===========================================================================
 * MISAFIR SITESI UST BILGISI — sabit, tek satirlik cubuk
 * ===========================================================================
 *
 * SABITLEME. Cubuk `.hcg-header` ile `position: sticky; top: 0` durur (gerekce
 * ve tuzaklar styles.css'te). Bu bilesenin sorumlulugu yalnizca ICERIKTIR;
 * konumlandirma tek bir CSS sinifinda toplanmistir.
 *
 * ---------------------------------------------------------------------------
 * NEDEN ARTIK TEK SATIR (onceki karar neden bozuldu)
 * ---------------------------------------------------------------------------
 * Onceki duzen iki satirdi ve mobilde uce sariyordu (marka / dil + eylem /
 * gezinme). Akista duran bir ust bilgi icin bu savunulabilirdi: yer yiyordu ama
 * bir kez yiyordu, sonra yukari kayip gidiyordu. Cubuk SABITLENINCE ayni uc
 * satir ~168px'i KALICI olarak isgal eder — 375x667 bir telefonda ekranin
 * dortte biri. Yani sabitleme karari, mobil duzeni yeniden acmak zorundaydi.
 *
 * Yeni desen (Booking/Airbnb'nin mobil kaliba yaklasimi, gorsel dil bizde
 * kalarak): **tek satir, yogun cubuk**.
 *
 *   < 64rem : [ marka ] ................. [ JETZT BUCHEN ] [ menu ]   56px
 *   >= 64rem: [ marka ] [ gezinme ] ..... [ DE EN TR ] [ JETZT BUCHEN ] 64px
 *
 * ESIK NEDEN 64rem (lg), 48rem (md) DEGIL: dil secici ile gezinme ayni anda
 * gorunmeli ya da ayni anda menuye girmelidir. 768px'te ucunu birden (gezinme +
 * uc dil + eylem) satira sokmak, uzun dizelerde (tr "Odalar ve fiyatlar",
 * "Rezervasyon") tasma riski demektir. Tek esik, "dil gorunur ama menu dugmesi
 * yok" gibi arada kalmis bir durumu de imkansiz kilar.
 *
 * SATIRDA NE KALDI, NE MENUYE GITTI:
 *  - Marka (monogram + ad): kimlik; her boyutta kalir. "Hotel & Restaurant"
 *    ust satiri (eyebrow) DUSTU — tek satirlik cubukta ikinci bir tipografik
 *    kat yalnizca yukseklik yer; ayni metin alt bilgide durmaya devam eder.
 *  - Birincil eylem ("Jetzt buchen"): rezervasyon sitesinin tek isi. Kalici
 *    olarak gorunur olmasinin bedeli 56px'lik bir seride ~90px yatay alandir;
 *    karsiligi, sayfanin her yerinden tek dokunusla huniye girilmesidir.
 *  - Gezinme (2 baglanti) ve dil secici (3 baglanti): menuye. Bunlar bir
 *    ZIYARETTE EN FAZLA BIR KEZ kullanilan denetimlerdir; birincil eylemi
 *    onlarin yanina sikistirmak, her ikisini de kucultmek anlamina gelirdi.
 *  - DIL SECICI OZELLIKLE: gorunurlugunu kaybetmesi bedelsiz degil. Karsiligi
 *    (a) menu dugmesinin erisilebilir adi ceviriden gelir ve panel acildiginda
 *    dil listesi tam adlariyla ("Deutsch / Englisch / Türkisch") gorunur —
 *    dar cubuktaki uc harfli kodlardan daha okunur, (b) `<html lang>` ve
 *    `hreflang` sinyalleri degismedigi icin arama motoru ve tarayici cevirisi
 *    etkilenmez, (c) alt bilgideki dil sutunu oldugu gibi durur, yani ikinci
 *    bir yol her zaman acik.
 *
 * KUCULME / GIZLENME: yok. Cubuk sabit yukseklikte kalir.
 *  - "Asagi kaydirinca gizlen" bir kaydirma dinleyicisi ister; SSR ciktisinda
 *    olcu yoktur, hidrasyondan once davranis farklidir ve kullanicinin altina
 *    dogru geri gelen bir cubuk, odaklanmis bir ogenin uzerine binebilir —
 *    yani `scroll-padding-top` disiplinini gecersiz kilar.
 *  - "Kaydirdikca kucul" ust bilgi yuksekligini degisken yapar; o yukseklikten
 *    turetilen her sey (capa dolgusu, yapiskan yan sutun hattı) yaniltici olur.
 *  - Ve rezervasyon sitesinde asagi kaydirmak, kullanicinin okuyup KARARA
 *    yaklastigi andir; birincil eylemi tam o anda gizlemek yanlis takas.
 *
 * ARAMA OZETI (tarih/kisi) CUBUKTA GOSTERILMIYOR — gerekce:
 *  Booking bunu yapar cunku sonuc listesi yuzlerce satirdir ve arama formu
 *  sonuc sayfasinda GORUNMEZ; yogunlastirilmis ozet oradaki tek hatirlaticidir.
 *  Bizde durum tersi: `/search` sayfasi formu (secili degerlerle dolu olarak)
 *  sayfanin tepesinde tutar, sorgu ADRESTEDIR ve sonuc kumesi tek bir evin
 *  birkac oda tipidir — kullanici formdan birkac ekran otesine gitmez. Ozeti
 *  cubuga kopyalamak (a) ayni durumun ikinci bir kaynagini yaratir, (b) 375px'te
 *  tam da yeni kazandigimiz satiri geri alir ve birincil eylemi disari iter,
 *  (c) her sayfada tekrarlanan bir landmark icine ekran okuyucu icin gurultu
 *  ekler. Ihtiyac ("ne aradigimi kaybetmeyeyim") zaten sonuc basliginin yanindaki
 *  `search-summary` satiriyla karsilaniyor.
 *
 * Marka isareti `@hotelcore/shared` katmanindan gelir — panelle ayni monogram.
 */
@Component({
  selector: 'hcg-guest-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, BrandMark],
  host: {
    /*
     * SABITLEME BILESENIN KENDI ETIKETINDEDIR, icteki <header> de degil — ve
     * bu bir zevk meselesi degil, olculmus bir hatanin duzeltmesidir.
     *
     * `position: sticky` bir ogeyi yalnizca KENDI KAPSAYICI BLOGU icinde
     * tutabilir. Sinif icteki <header>'a verildiginde kapsayici blok
     * <hcg-guest-header> etiketi olur; o etiketin kutusu da tam olarak
     * header'in yuksekligi kadardir (57px). Yani yapisma araligi SIFIRDIR:
     * hesaplanan stil "sticky, top: 0" der, DevTools dogrular, ama sayfa
     * kaydirilinca cubuk yukari kayip gider. Gercek tarayicida olculdu:
     * 1200px kaydirmadan sonra getBoundingClientRect().top = -1200.
     *
     * Sinif kabuktaki dikey flex kabinin dogrudan cocugu olan BU etikete
     * verilince kapsayici blok tum sayfa yuksekligindeki kap olur ve cubuk
     * belge sonuna kadar tepede kalir.
     */
    class: 'hcg-header',

    /*
     * Escape dinleyicisi de burada, sablondaki bir <div>'de degil. Iki sebep:
     * (a) menu acikken odak ya tetikleyicidedir ya da panelin icindedir; ikisi
     * de bu etiketin altindadir, yani olay her durumda buraya kabarir,
     * (b) sablonda dursaydi "etkilesim isleyicisi olan oge odaklanabilir olmali"
     * kurali uyarirdi — o kural tiklama isleyicileri icindir ama kapsayici bir
     * Escape dinleyicisini ondan ayirt edemez.
     */
    '(keydown.escape)': 'closeMenu(true)',
  },
  template: `
    <header class="border-b border-rule bg-canvas" data-testid="guest-header">
      <div class="hcg-shell flex min-h-header items-center gap-3 lg:min-h-header-wide lg:gap-8">
        <a
          [routerLink]="homePath()"
          class="mr-auto flex shrink-0 items-center gap-2.5 text-ink no-underline lg:gap-3"
          data-testid="header-brand"
        >
          <hc-brand-mark [size]="28" [label]="'common.appName' | translate" />
          <span class="font-serif text-lg leading-none lg:text-2xl" aria-hidden="true">
            {{ 'common.appName' | translate }}
          </span>
        </a>

        <!--
          Genis ekran gezinmesi. "hidden lg:block" — yani DOM'da HER ZAMAN
          vardir. Sebep: SSR ciktisinda gezinme baglantilarinin bulunmasi
          taranabilirlik icin gerekli; menu paneli ise yalnizca acilinca
          cizildigi icin sunucu HTML'i baglantilari TEK kez tasir (ayni
          baglantinin iki kopyasi crawler icin gurultudur).
        -->
        <nav
          class="hidden lg:block"
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

        <!--
          Dil secici: baglanti (buton degil). Her dilin kendi ADRESI oldugu icin
          bunlar gercek gezinmelerdir; "hreflang" + "lang" nitelikleri hem
          ekran okuyucuya hem tarayiciya dogru sinyali verir.
        -->
        <nav
          class="hidden lg:block"
          [attr.aria-label]="'nav.languageNavigation' | translate"
          data-testid="language-switch"
        >
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

        <!--
          Birincil eylem. Etiket kirilima gore DEGISIR ve bu bir sus degil,
          olcum sonucudur: 375px'te tr "Hemen rezervasyon" tek basina ~170px
          yer ister ve marka + menu dugmesiyle birlikte satiri tasirir. Kisa
          bicim ayri bir anahtardan gelir (nav.bookShort) — sablonda kirpma
          ya da CSS ile metin kisaltma yok; kisa bicimi kendi dilinde yazan
          cevirmendir. Olculdu: tr kisa bicim ("Rezerve et") 113.6px.
        -->
        <a
          [routerLink]="searchPath()"
          class="hcg-action hcg-action--compact shrink-0 lg:min-h-action lg:px-6 lg:text-xs"
          data-testid="header-cta"
        >
          <span class="lg:hidden">{{ 'nav.bookShort' | translate }}</span>
          <span class="hidden lg:inline">{{ 'nav.book' | translate }}</span>
        </a>

        <!--
          Menu tetikleyicisi. Ikon stok bir setten degil: uc yatay cetvel
          cizgisi (defter satirlari) / kapanista iki capraz cizgi — panelde de
          ayni dil kullaniliyor. Gorunur metni yoktur, bu yuzden erisilebilir
          ad ceviriden gelir ve durumla birlikte degisir.
        -->
        <button
          #menuToggle
          type="button"
          class="flex touch-target shrink-0 items-center justify-center border border-rule text-ink hover:border-rule-strong hover:bg-canvas-deep lg:hidden"
          [attr.aria-expanded]="menuOpen()"
          aria-controls="hcg-header-menu"
          [attr.aria-label]="(menuOpen() ? 'nav.closeMenu' : 'nav.openMenu') | translate"
          data-testid="menu-toggle"
          (click)="toggleMenu()"
        >
          @if (menuOpen()) {
            <svg
              class="hcg-icon"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
              focusable="false"
              data-testid="icon-close"
            >
              <path d="M4 4l8 8M12 4l-8 8" />
            </svg>
          } @else {
            <svg
              class="hcg-icon"
              viewBox="0 0 16 16"
              fill="none"
              aria-hidden="true"
              focusable="false"
              data-testid="icon-menu"
            >
              <path d="M2.5 4.5h11M2.5 8h11M2.5 11.5h11" />
            </svg>
          }
        </button>
      </div>

      <!--
        Panel ust bilginin ICINDE durur: boylece yapiskan kutunun bir parcasidir
        ve cubukla birlikte tepede kalir; ayri bir sabit (fixed) katman, ust cubugun
        yuksekligini elle tekrarlamayi gerektirirdi.

        Kipli (modal) DEGILDIR: odak tuzagi yok, arka plan gizlenmiyor. Iki
        gezinme + uc dil baglantisi icin kipli bir katman fazla agir olurdu;
        DOM sirasi tetikleyiciden hemen sonra geldigi icin sekme sirasi zaten
        dogru. Escape kapatir ve odagi tetikleyiciye geri verir.
      -->
      @if (menuOpen()) {
        <div
          id="hcg-header-menu"
          class="hcg-header-menu border-t border-rule lg:hidden"
          data-testid="header-menu"
        >
          <div class="hcg-shell py-4">
            <nav [attr.aria-label]="'nav.mainNavigation' | translate">
              <ul class="border-t border-rule">
                @for (item of navigation(); track item.path) {
                  <li class="border-b border-rule">
                    <a
                      [routerLink]="item.path"
                      [attr.aria-current]="item.active ? 'page' : null"
                      class="flex touch-target items-center text-sm text-ink no-underline aria-[current=page]:text-copper"
                      [attr.data-testid]="'menu-nav-' + item.testId"
                      (click)="closeMenu()"
                    >
                      {{ item.labelKey | translate }}
                    </a>
                  </li>
                }
              </ul>
            </nav>

            <p class="mt-6 eyebrow" id="hcg-menu-language-label">
              {{ 'nav.language' | translate }}
            </p>
            <nav aria-labelledby="hcg-menu-language-label" data-testid="menu-language-switch">
              <ul class="mt-2 border-t border-rule">
                @for (language of languages; track language) {
                  <li class="border-b border-rule">
                    <a
                      [routerLink]="urlFor(language)"
                      [attr.hreflang]="language"
                      [attr.lang]="language"
                      [attr.aria-current]="language === current() ? 'true' : null"
                      class="flex touch-target items-center gap-3 text-sm text-ink no-underline aria-[current=true]:text-copper"
                      [attr.data-testid]="'menu-language-link-' + language"
                      (click)="closeMenu()"
                    >
                      <span class="label-mono text-ink-faint">{{ language }}</span>
                      <span>{{ 'language.' + language | translate }}</span>
                    </a>
                  </li>
                }
              </ul>
            </nav>
          </div>
        </div>
      }
    </header>
  `,
  styles: `
    /*
     * Ikon cizimi — panelin .hc-icon deseniyle ayni cetvel dili: 1.25 kalinlik,
     * kesin uclar, yuvarlatma yok. Yerlesim/gorunurluk yardimcilari sablonda
     * kalir; bilesen stilleri oznitelik seciciyle yazildigi icin lg:hidden
     * gibi yardimcilari ezerdi.
     */
    .hcg-icon {
      width: 1.125rem;
      height: 1.125rem;
      stroke: currentColor;
      stroke-width: 1.25;
      stroke-linecap: butt;
      stroke-linejoin: miter;
      shape-rendering: geometricPrecision;
    }
  `,
})
export class GuestHeader {
  private readonly language = inject(LanguageStore);
  private readonly currentUrl = inject(CurrentUrlStore);
  private readonly menuToggle = viewChild<ElementRef<HTMLButtonElement>>('menuToggle');

  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly current = this.language.current;

  private readonly _menuOpen = signal(false);
  /**
   * SSR'da her zaman KAPALI baslar; sunucu HTML'i ile hidrasyon sonrasi ilk
   * cerceve birebir aynidir, dolayisiyla panel yuzunden bir yerlesim kaymasi
   * olusamaz.
   */
  protected readonly menuOpen = this._menuOpen.asReadonly();

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

  constructor() {
    /*
     * Gezinme olunca panel kapanir. Baglantinin kendi (click) isleyicisi ilk
     * savunmadir; bu efekt ikinci savunmadir ve tarayicinin geri/ileri tuslari,
     * yonlendirmeler ve dil degistirme gibi TIKLAMASIZ gezinmeleri de kapsar.
     */
    effect(() => {
      this.currentUrl.url();
      this._menuOpen.set(false);
    });
  }

  protected toggleMenu(): void {
    this._menuOpen.update((open) => !open);
  }

  /** `restoreFocus`: Escape ile kapatilinca odak tetikleyiciye geri doner. */
  protected closeMenu(restoreFocus = false): void {
    if (!this._menuOpen()) {
      return;
    }
    this._menuOpen.set(false);
    if (restoreFocus) {
      this.menuToggle()?.nativeElement.focus();
    }
  }

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
