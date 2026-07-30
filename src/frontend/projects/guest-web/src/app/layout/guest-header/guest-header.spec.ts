import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { beforeEach, describe, expect, it } from 'vitest';

import { BrandMark, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { configureGuestTestBed, renderRoute as render } from '../../../testing/guest-test-bed';
import { GuestHeader } from './guest-header';

beforeEach(() => configureGuestTestBed({ acceptLanguage: 'de' }));

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
    expect(cta?.textContent?.trim()).toBe('nav.book');
  });
});
