import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { Topbar } from './topbar';

function render() {
  const fixture = TestBed.createComponent(Topbar);
  fixture.detectChanges();

  const element = fixture.nativeElement as HTMLElement;

  return {
    fixture,
    element,
    setMenuOpen(open: boolean) {
      fixture.componentRef.setInput('menuOpen', open);
      fixture.detectChanges();
    },
    header(): HTMLElement | null {
      return element.querySelector('header');
    },
    brand(): HTMLElement | null {
      return element.querySelector<HTMLElement>('[data-testid="topbar-brand"]');
    },
    menuButton(): HTMLButtonElement | null {
      return element.querySelector<HTMLButtonElement>('[data-testid="menu-toggle"]');
    },
  };
}

beforeEach(() => {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([]),
      provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
    ],
  });
});

describe('Topbar — marka header in en solunda', () => {
  it('marka blogunu header in **ilk** ogesi olarak cizer', () => {
    const view = render();

    // Duzen istegi: "logo header'in en solunda". DOM sirasi = gorsel sira
    // (header duz bir flex satiri; marka blogunda `order-*` yardimcisi yok).
    expect(view.header()?.firstElementChild).toBe(view.brand());
  });

  it('markayi her ekran boyutunda gosterir (eski `lg:hidden` kisiti kalkti)', () => {
    const view = render();
    const classes = view.brand()?.className ?? '';

    expect(classes).not.toContain('hidden');
    expect(classes).not.toContain('lg:hidden');
    expect(view.brand()?.querySelector('[data-testid="brand-mark"]')).not.toBeNull();
  });

  it('erisilebilir adi isaret tasir, gorunur metin tekrardir ve i18n den gelir', () => {
    const view = render();
    const mark = view.brand()?.querySelector('[data-testid="brand-mark"]');

    // 375px'te ad metni gizlendigi icin ad isarette durmali.
    expect(mark?.getAttribute('role')).toBe('img');
    // Ceviri dosyasi birim testte yuklenmedigi icin `translate` anahtari dondurur;
    // dogrulanan sey **adin koda gomulmedigi**, `common.appName` den geldigi.
    expect(mark?.getAttribute('aria-label')).toBe('common.appName');
    // Gorunur ad ayni bilgiyi tekrar ettigi icin ekran okuyucudan gizlenir.
    expect(view.brand()?.querySelector('p[aria-hidden="true"]')?.textContent?.trim()).toBe(
      'common.appName',
    );
  });

  it('sol dolguyu `lg` de kenar cubugu kalemleriyle ayni hatta ceker', () => {
    const view = render();
    const classes = view.header()?.className ?? '';

    // Kenar cubugu kalemleri `px-4`; header `lg:pl-4` ile ayni dikey hatta oturur.
    expect(classes).toContain('lg:pl-4');
  });
});

describe('Topbar — mobil menu dugmesi', () => {
  it('kapaliyken cetvel satirlari, acikken capraz ikon cizer', () => {
    const view = render();

    expect(view.menuButton()?.textContent).not.toMatch(/[≡✕]/);
    expect(view.menuButton()?.querySelector('[data-testid="icon-menu"]')).not.toBeNull();

    view.setMenuOpen(true);
    expect(view.menuButton()?.querySelector('[data-testid="icon-close"]')).not.toBeNull();
    expect(view.menuButton()?.getAttribute('aria-expanded')).toBe('true');
    expect(view.menuButton()?.getAttribute('aria-label')).toBe('nav.closeMenu');
  });

  it('markadan sonra gelir; marka en solda kalir', () => {
    const view = render();

    expect(view.brand()?.nextElementSibling).toBe(view.menuButton());
  });
});

describe('Topbar — tasinan denetimler', () => {
  it('kenar cubugu daraltma dugmesini artik barindirmaz (kenar cubuguna tasindi)', () => {
    const view = render();

    expect(view.element.querySelector('[data-testid="sidebar-toggle"]')).toBeNull();
  });

  it('dil seciciyi artik barindirmaz (dil Ayarlar ekranindan secilir)', () => {
    const view = render();

    expect(view.element.querySelector('hc-language-picker')).toBeNull();
  });

  it('otel secici ve kullanici menusu ust cubukta kalir', () => {
    const view = render();

    expect(view.element.querySelector('hc-hotel-switcher')).not.toBeNull();
    expect(view.element.querySelector('hc-user-menu')).not.toBeNull();
  });
});
