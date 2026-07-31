import { ApplicationRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { beforeEach, describe, expect, it } from 'vitest';

import { BrandMark, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { configureGuestTestBed, renderRoute as render } from '../../../testing/guest-test-bed';
import { GuestHeader } from './guest-header';

beforeEach(() => configureGuestTestBed({ acceptLanguage: 'de' }));

/** Ust bilgiyi tek basina kurar — etkilesim testleri icin. */
function mountHeader(): ComponentFixture<GuestHeader> {
  const fixture = TestBed.createComponent(GuestHeader);
  fixture.detectChanges();
  return fixture;
}

function query<T extends Element>(fixture: ComponentFixture<GuestHeader>, testId: string) {
  return (fixture.nativeElement as HTMLElement).querySelector<T>(`[data-testid="${testId}"]`);
}

function clickToggle(fixture: ComponentFixture<GuestHeader>): void {
  query<HTMLButtonElement>(fixture, 'menu-toggle')?.click();
  fixture.detectChanges();
}

describe('Ust bilgi — marka', () => {
  it('marka isaretini PAYLASILAN kutuphaneden alir (kopya bilesen yok)', () => {
    /*
     * Bu testin amaci: misafir sitesinin kendi monogramini cizmeye baslamasini
     * engellemek. `By.directive(BrandMark)` yalnizca `@hotelcore/shared`
     * icindeki SINIFIN TA KENDISI render edilmisse eslesir; birebir ayni
     * sablona sahip yerel bir kopya bu testi gecemez.
     */
    const fixture = TestBed.createComponent(GuestHeader);
    fixture.detectChanges();

    const mark = fixture.debugElement.query(By.directive(BrandMark));
    expect(mark).not.toBeNull();
    expect(mark.componentInstance).toBeInstanceOf(BrandMark);
  });

  it('markayi cizer ve erisilebilir ad i18n anahtarindan gelir', async () => {
    const element = await render('/de');
    const mark = element.querySelector('[data-testid="brand-mark"]');

    expect(mark).not.toBeNull();
    expect(mark?.getAttribute('role')).toBe('img');
    expect(mark?.getAttribute('aria-label')).toBe('common.appName');
  });

  it('marka baglantisi aktif dilin ana sayfasina gider', async () => {
    const element = await render('/tr/search');
    expect(element.querySelector('[data-testid="header-brand"]')?.getAttribute('href')).toBe('/tr');
  });

  it('marka blogu TEK satirdir: "tagline" ust bilgide cizilmez', () => {
    /*
     * Ust bilgi sabit oldugu icin yuksekligi kalici bir maliyettir. Marka
     * altindaki ikinci tipografik kat (Hotel & Restaurant) bu yuzden alt
     * bilgiye birakildi. Test, birinin onu "sadece bir satir" diye geri
     * koymasini engeller.
     */
    const fixture = mountHeader();
    const brand = query<HTMLElement>(fixture, 'header-brand');

    expect(brand?.textContent).toContain('common.appName');
    expect(brand?.textContent).not.toContain('common.tagline');
  });
});

describe('Ust bilgi — sabitleme', () => {
  it('sabitleme sinifi BILESEN ETIKETINDE durur, icteki <header> de DEGIL', () => {
    /*
     * REGRESYON TESTI — gercek tarayicida olculmus bir hata.
     * `position: sticky` bir ogeyi yalnizca kendi KAPSAYICI BLOGU icinde
     * tutar. Sinif icteki <header>'a verilirse kapsayici blok
     * <hcg-guest-header> etiketi olur; o kutu tam header yuksekligindedir,
     * yani yapisma araligi sifirdir. Hesaplanan stil dogru gorunur ("sticky,
     * top: 0") ama sayfa kaydirilinca cubuk yukari kayip gider.
     * Sinif kabuktaki dikey flex kabinin dogrudan cocugu olan bilesen
     * etiketinde olmali.
     */
    const fixture = mountHeader();
    const host = fixture.nativeElement as HTMLElement;
    const header = query<HTMLElement>(fixture, 'guest-header');

    // TestBed konak ogesini kendisi uretir; onemli olan sinifin KONAKTA olmasi.
    // Konagin gercekten <hcg-guest-header> olup kabuktaki flex kabin dogrudan
    // cocugu oldugu guest-shell.spec.ts'te dogrulanir.
    expect(host.classList.contains('hcg-header')).toBe(true);

    expect(header?.tagName).toBe('HEADER');
    expect(header?.classList.contains('hcg-header')).toBe(false);
  });

  it('opak zemin tasir (altindan gecen icerik cubukta okunmaz)', () => {
    const fixture = mountHeader();
    expect(query<HTMLElement>(fixture, 'guest-header')?.classList.contains('bg-canvas')).toBe(true);
  });

  it('cubuk yuksekligi TOKENDAN gelir (capa dolgusuyla ayni kaynak)', () => {
    /*
     * `scroll-padding-top` ve yapiskan yan sutunlar `--spacing-header` /
     * `--spacing-header-wide` uzerinden hesaplanir. Yukseklik sablona elle
     * yazilirsa (`h-14` gibi) o hesaplar sessizce yanlislasir.
     */
    const fixture = mountHeader();
    const bar = query<HTMLElement>(fixture, 'guest-header')?.firstElementChild;

    expect(bar?.classList.contains('min-h-header')).toBe(true);
    expect(bar?.classList.contains('lg:min-h-header-wide')).toBe(true);
  });
});

describe('Ust bilgi — dil secici', () => {
  it('uc dil icin de baglanti uretir, hreflang ve lang nitelikleriyle', async () => {
    const element = await render('/de');

    for (const language of SUPPORTED_LANGUAGES) {
      const link = element.querySelector<HTMLAnchorElement>(
        `[data-testid="language-link-${language}"]`,
      );
      expect(link, `${language} baglantisi yok`).not.toBeNull();
      expect(link?.getAttribute('hreflang')).toBe(language);
      expect(link?.getAttribute('lang')).toBe(language);
    }
  });

  it('dil degistirirken kullaniciyi AYNI sayfada tutar', async () => {
    const element = await render('/de/legal/terms');

    expect(element.querySelector('[data-testid="language-link-tr"]')?.getAttribute('href')).toBe(
      '/tr/legal/terms',
    );
    expect(element.querySelector('[data-testid="language-link-en"]')?.getAttribute('href')).toBe(
      '/en/legal/terms',
    );
  });

  it('aktif dili aria-current ile isaretler', async () => {
    const element = await render('/en');

    expect(
      element.querySelector('[data-testid="language-link-en"]')?.getAttribute('aria-current'),
    ).toBe('true');
    expect(
      element.querySelector('[data-testid="language-link-de"]')?.getAttribute('aria-current'),
    ).toBeNull();
  });

  it('dil secici SUNUCU HTML inde de vardir (yalnizca CSS ile gizlenir)', () => {
    /*
     * Dar ekranda dil secici gorunmez ama DOM'dan SILINMEZ: `hreflang`
     * baglantilari arama motorunun dil alternatiflerini bulma yoludur ve
     * bunlar SSR ciktisinda bulunmalidir. Menu paneli ise yalnizca acilinca
     * cizilir; boylece sunucu HTML'i her baglantiyi TEK kez tasir.
     */
    const fixture = mountHeader();
    const nav = query<HTMLElement>(fixture, 'language-switch');

    expect(nav).not.toBeNull();
    expect(nav?.classList.contains('hidden')).toBe(true);
    expect(nav?.classList.contains('lg:block')).toBe(true);
    expect(query(fixture, 'menu-language-link-de')).toBeNull();
  });
});

describe('Ust bilgi — gezinme ve eylem', () => {
  it('gezinme baglantilari dil on ekli, aktif olan aria-current tasir', async () => {
    const element = await render('/de');

    expect(element.querySelector('[data-testid="nav-home"]')?.getAttribute('href')).toBe('/de');
    expect(element.querySelector('[data-testid="nav-rooms"]')?.getAttribute('href')).toBe(
      '/de/search',
    );
    expect(element.querySelector('[data-testid="nav-home"]')?.getAttribute('aria-current')).toBe(
      'page',
    );
  });

  it('birincil eylem arama sayfasina gider ve sabit metin icermez', async () => {
    const element = await render('/de');
    const cta = element.querySelector<HTMLAnchorElement>('[data-testid="header-cta"]');

    expect(cta?.getAttribute('href')).toBe('/de/search');
    // NoOp yukleyici anahtari dondurur: metin sablona gomulmemis demektir.
    expect(cta?.textContent).toContain('nav.book');
  });

  it('birincil eylemin dar ekran icin KISA bir etiketi vardir', () => {
    /*
     * Uzun etiket (tr: "Hemen rezervasyon") 375px'te marka ve menu dugmesiyle
     * birlikte satiri tasirir. Kisa bicim ayri bir anahtardir; CSS ile kirpma
     * ya da `text-overflow` YOKTUR.
     */
    const fixture = mountHeader();
    const cta = query<HTMLElement>(fixture, 'header-cta');
    const labels = [...(cta?.querySelectorAll('span') ?? [])];

    const short = labels.find((span) => span.textContent?.trim() === 'nav.bookShort');
    const long = labels.find((span) => span.textContent?.trim() === 'nav.book');

    expect(short?.className).toContain('lg:hidden');
    expect(long?.className).toContain('hidden');
    expect(long?.className).toContain('lg:inline');
  });

  it('eylem dokunmatik alt sinirin altina inmez (sikistirilmis bicimde de)', () => {
    const fixture = mountHeader();
    const cta = query<HTMLElement>(fixture, 'header-cta');

    // .hcg-action--compact: min-height = --spacing-touch (2.75rem = 44px)
    expect(cta?.classList.contains('hcg-action--compact')).toBe(true);
  });
});

describe('Ust bilgi — dar ekran menusu', () => {
  it('menu dugmesi yalnizca dar ekranda gorunur ve dokunmatik olcudedir', () => {
    const fixture = mountHeader();
    const toggle = query<HTMLButtonElement>(fixture, 'menu-toggle');

    expect(toggle?.getAttribute('type')).toBe('button');
    expect(toggle?.classList.contains('lg:hidden')).toBe(true);
    expect(toggle?.classList.contains('touch-target')).toBe(true);
  });

  it('kapaliyken panel DOM da YOKTUR ve aria-expanded false tir', () => {
    const fixture = mountHeader();
    const toggle = query<HTMLButtonElement>(fixture, 'menu-toggle');

    expect(toggle?.getAttribute('aria-expanded')).toBe('false');
    expect(toggle?.getAttribute('aria-controls')).toBe('hcg-header-menu');
    expect(toggle?.getAttribute('aria-label')).toBe('nav.openMenu');
    expect(query(fixture, 'header-menu')).toBeNull();
  });

  it('acildiginda panel cizilir, durum ve erisilebilir ad birlikte doner', () => {
    const fixture = mountHeader();
    clickToggle(fixture);

    const toggle = query<HTMLButtonElement>(fixture, 'menu-toggle');
    const panel = query<HTMLElement>(fixture, 'header-menu');

    expect(toggle?.getAttribute('aria-expanded')).toBe('true');
    expect(toggle?.getAttribute('aria-label')).toBe('nav.closeMenu');
    expect(panel?.id).toBe('hcg-header-menu');
  });

  it('panel cubuktan cikan HER SEYI tasir: gezinme + dil', () => {
    const fixture = mountHeader();
    clickToggle(fixture);

    expect(query<HTMLAnchorElement>(fixture, 'menu-nav-home')?.getAttribute('href')).toBe('/de');
    expect(query<HTMLAnchorElement>(fixture, 'menu-nav-rooms')?.getAttribute('href')).toBe(
      '/de/search',
    );

    for (const language of SUPPORTED_LANGUAGES) {
      const link = query<HTMLAnchorElement>(fixture, `menu-language-link-${language}`);
      expect(link, `${language} menude yok`).not.toBeNull();
      expect(link?.getAttribute('hreflang')).toBe(language);
      expect(link?.classList.contains('touch-target')).toBe(true);
    }
  });

  it('menudeki dil listesi tam adlari gosterir ve aktif olani isaretler', () => {
    const fixture = mountHeader();
    clickToggle(fixture);

    const active = query<HTMLAnchorElement>(fixture, 'menu-language-link-de');
    expect(active?.getAttribute('aria-current')).toBe('true');
    expect(active?.textContent).toContain('language.de');

    expect(
      query<HTMLAnchorElement>(fixture, 'menu-language-link-en')?.getAttribute('aria-current'),
    ).toBeNull();
  });

  it('dil grubunun erisilebilir adi gorunur bir baslikla baglanir', () => {
    const fixture = mountHeader();
    clickToggle(fixture);

    const nav = query<HTMLElement>(fixture, 'menu-language-switch');
    const labelId = nav?.getAttribute('aria-labelledby');
    expect(labelId).toBe('hcg-menu-language-label');

    const label = (fixture.nativeElement as HTMLElement).querySelector(`#${labelId}`);
    expect(label?.textContent?.trim()).toBe('nav.language');
  });

  it('Escape kapatir ve odagi tetikleyiciye geri verir', () => {
    const fixture = mountHeader();
    clickToggle(fixture);

    const link = query<HTMLAnchorElement>(fixture, 'menu-nav-home');
    link?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(query(fixture, 'header-menu')).toBeNull();
    expect(document.activeElement).toBe(query<HTMLButtonElement>(fixture, 'menu-toggle'));
  });

  it('ZATEN ACIK olan sayfanin baglantisina tiklaninca da kapanir', async () => {
    /*
     * Bu, efektin yakalayamadigi tek durumdur: ayni adrese gezinme router
     * tarafindan yok sayilir, `NavigationEnd` gelmez. Baglantidaki acik
     * (click) isleyicisi bu yuzden vardir — kullanici "Startseite"ye basip
     * menunun acik kalmasiyla karsilasmaz.
     */
    const element = await render('/de');
    const app = TestBed.inject(ApplicationRef);

    element.querySelector<HTMLButtonElement>('[data-testid="menu-toggle"]')?.click();
    app.tick();
    expect(element.querySelector('[data-testid="header-menu"]')).not.toBeNull();

    element.querySelector<HTMLAnchorElement>('[data-testid="menu-nav-home"]')?.click();
    app.tick();
    expect(element.querySelector('[data-testid="header-menu"]')).toBeNull();
  });

  it('TIKLAMASIZ gezinme de kapatir (geri tusu, yonlendirme)', async () => {
    const element = await render('/de');
    const app = TestBed.inject(ApplicationRef);

    element.querySelector<HTMLButtonElement>('[data-testid="menu-toggle"]')?.click();
    app.tick();
    expect(element.querySelector('[data-testid="header-menu"]')).not.toBeNull();

    await render('/de/search');
    expect(element.querySelector('[data-testid="header-menu"]')).toBeNull();
  });
});
