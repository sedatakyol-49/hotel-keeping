import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { beforeEach, describe, expect, it } from 'vitest';

import { BrandMark, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { configureGuestTestBed, renderRoute as render } from '../../../testing/guest-test-bed';
import { LEGAL_DOCUMENTS } from '../../features/legal/legal-documents';
import { GuestFooter } from './guest-footer';

beforeEach(() => configureGuestTestBed({ acceptLanguage: 'de' }));

describe('Alt bilgi — hukuki baglantilar', () => {
  it('tek kaynak listedeki TUM belgeler icin baglanti uretir', async () => {
    const element = await render('/de');
    const links = element.querySelectorAll('[data-testid^="legal-link-"]');

    // Sayi esitligi: listeye belge eklenip alt bilgi guncellenmezse test kirilir.
    expect(links).toHaveLength(LEGAL_DOCUMENTS.length);
  });

  it('baglanti metinleri i18n anahtarindan gelir (sabit metin yok)', async () => {
    const element = await render('/de');

    for (const document_ of LEGAL_DOCUMENTS) {
      const link = element.querySelector(`[data-testid="legal-link-${document_.slug}"]`);
      expect(link?.textContent?.trim()).toBe(document_.labelKey);
    }
  });

  it('hukuki gezinme kendi landmark adini tasir', async () => {
    const element = await render('/de');
    const nav = element.querySelector('footer nav[aria-label="footer.legalNavigation"]');
    expect(nav).not.toBeNull();
  });
});

describe('Alt bilgi — dil ve marka', () => {
  it('uc dil icin de baglanti verir ve bulunulan sayfayi korur', async () => {
    const element = await render('/de/search');

    for (const language of SUPPORTED_LANGUAGES) {
      const link = element.querySelector<HTMLAnchorElement>(
        `[data-testid="footer-language-${language}"]`,
      );
      expect(link?.getAttribute('href')).toBe(`/${language}/search`);
      expect(link?.getAttribute('hreflang')).toBe(language);
    }
  });

  it('marka isareti paylasilan kutuphaneden gelir', () => {
    const fixture = TestBed.createComponent(GuestFooter);
    fixture.detectChanges();

    expect(fixture.debugElement.query(By.directive(BrandMark))).not.toBeNull();
  });

  it('telif satirinda yil i18n parametresi olarak verilir', async () => {
    const element = await render('/de');
    // NoOp yukleyici anahtari dondurur; onemli olan metnin sablona gomulmemesi.
    expect(element.querySelector('footer')?.textContent).toContain('footer.copyright');
  });
});
