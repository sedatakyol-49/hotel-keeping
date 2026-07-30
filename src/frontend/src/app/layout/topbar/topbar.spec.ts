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
    setCollapsed(collapsed: boolean) {
      fixture.componentRef.setInput('sidebarCollapsed', collapsed);
      fixture.detectChanges();
    },
    setMenuOpen(open: boolean) {
      fixture.componentRef.setInput('menuOpen', open);
      fixture.detectChanges();
    },
    toggle(): HTMLButtonElement | null {
      return element.querySelector<HTMLButtonElement>('[data-testid="sidebar-toggle"]');
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

describe('Topbar — kenar cubugu daraltma dugmesi', () => {
  it('genis durumda "daralt" etiketiyle basili olmayan durumu bildirir', () => {
    const view = render();

    // Ceviri dosyasi birim testte yuklenmedigi icin `translate` anahtari dondurur;
    // dogrulanan sey **hangi anahtarin hangi duruma bagli oldugu**.
    expect(view.toggle()?.getAttribute('aria-pressed')).toBe('false');
    expect(view.toggle()?.getAttribute('aria-label')).toBe('nav.collapseSidebar');
    expect(view.toggle()?.getAttribute('title')).toBe('nav.collapseSidebar');
  });

  it('daraltilmis durumda "genislet" etiketine ve basili duruma gecer', () => {
    const view = render();
    view.setCollapsed(true);

    expect(view.toggle()?.getAttribute('aria-pressed')).toBe('true');
    expect(view.toggle()?.getAttribute('aria-label')).toBe('nav.expandSidebar');
    expect(view.toggle()?.getAttribute('title')).toBe('nav.expandSidebar');
  });

  it('ham « / » karakteri yerine satir ici SVG ikon cizer ve yonu duruma gore cevirir', () => {
    const view = render();

    expect(view.toggle()?.textContent).not.toMatch(/[«»]/);
    expect(view.toggle()?.querySelector('svg')).not.toBeNull();
    expect(view.toggle()?.querySelector('[data-testid="icon-panel-collapse"]')).not.toBeNull();

    view.setCollapsed(true);
    expect(view.toggle()?.querySelector('[data-testid="icon-panel-expand"]')).not.toBeNull();
    expect(view.toggle()?.querySelector('[data-testid="icon-panel-collapse"]')).toBeNull();
  });

  it('ikonu ekran okuyucudan gizler; ad yalnizca dugmede durur', () => {
    const view = render();
    const icon = view.toggle()?.querySelector('svg');

    expect(icon?.getAttribute('aria-hidden')).toBe('true');
    expect(icon?.getAttribute('focusable')).toBe('false');
  });

  it('dokunmatik hedef ve cetvel cerceve dilini korur', () => {
    const view = render();
    const classes = view.toggle()?.className ?? '';

    expect(classes).toContain('touch-target');
    expect(classes).toContain('border-rule');
  });

  it('kenar cubugu gizliyken dugme hic cizilmez', () => {
    const view = render();
    view.fixture.componentRef.setInput('sidebarToggleVisible', false);
    view.fixture.detectChanges();

    expect(view.toggle()).toBeNull();
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
});

describe('Topbar — marka', () => {
  it('mobil marka blogunda erisilebilir adi isaret tasir, gorunur metin tekrardir', () => {
    const view = render();
    const mark = view.element.querySelector('hc-brand-mark [data-testid="brand-mark"]');

    expect(mark).not.toBeNull();
    // 375px'te ad metni gizlendigi icin ad isarette durmali.
    expect(mark?.getAttribute('role')).toBe('img');
    expect(mark?.getAttribute('aria-label')).toBe('common.appName');
    // Gorunur ad ayni bilgiyi tekrar ettigi icin ekran okuyucudan gizlenir.
    expect(view.element.querySelector('p[aria-hidden="true"]')?.textContent?.trim()).toBe(
      'common.appName',
    );
  });
});
