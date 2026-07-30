import { beforeEach, describe, expect, it } from 'vitest';

import { LEGAL_DOCUMENTS } from '../../features/legal/legal-documents';
import { configureGuestTestBed, renderRoute as render } from '../../../testing/guest-test-bed';

beforeEach(() => configureGuestTestBed({ acceptLanguage: 'de' }));

describe('Kabuk — semantik iskelet', () => {
  it('atlama baglantisi, ust bilgi, main ve alt bilgi cizer', async () => {
    const element = await render('/de');

    const skip = element.querySelector<HTMLAnchorElement>('a.hc-skip-link');
    expect(skip?.getAttribute('href')).toBe('#content');

    expect(element.querySelector('header[data-testid="guest-header"]')).not.toBeNull();
    expect(element.querySelector('footer[data-testid="guest-footer"]')).not.toBeNull();
  });

  it('main landmark odaklanabilirdir (atlama baglantisi odagi tasisin)', async () => {
    const element = await render('/de');
    const main = element.querySelector<HTMLElement>('main#content');

    expect(main).not.toBeNull();
    expect(main?.getAttribute('tabindex')).toBe('-1');
  });

  it('sayfada tek bir <h1> vardir', async () => {
    for (const url of ['/de', '/de/search', '/de/legal/imprint', '/de/booking']) {
      const element = await render(url);
      expect(element.querySelectorAll('h1'), `${url} icin h1 sayisi`).toHaveLength(1);
    }
  });
});

describe('Kabuk — hukuki baglantilar her sayfada (§5 DDG)', () => {
  /*
   * Alt bilgi kabukta durdugu icin bu, tum sayfalar icin gecerli bir
   * dogrulamadir; yine de temsili bir kesit uzerinde acikca test edilir —
   * ileride bir sayfa kendi duzenini kurmaya kalkarsa test kirilsin.
   */
  const pages = [
    '/de',
    '/de/search',
    '/de/rooms/doppelzimmer',
    '/de/booking',
    '/de/confirmation/ABC123',
    '/de/legal/privacy',
    '/de/olmayan-sayfa',
  ];

  for (const url of pages) {
    it(`${url} sayfasinda Impressum/Datenschutz/AGB baglantilari bulunur`, async () => {
      const element = await render(url);

      for (const document_ of LEGAL_DOCUMENTS) {
        const link = element.querySelector<HTMLAnchorElement>(
          `[data-testid="legal-link-${document_.slug}"]`,
        );
        expect(link, `${document_.slug} eksik`).not.toBeNull();
        expect(link?.getAttribute('href')).toBe(`/de/legal/${document_.slug}`);
      }
    });
  }

  it('hukuki baglantilar aktif dilin on ekini tasir', async () => {
    const element = await render('/tr/search');
    expect(element.querySelector('[data-testid="legal-link-imprint"]')?.getAttribute('href')).toBe(
      '/tr/legal/imprint',
    );
  });
});
