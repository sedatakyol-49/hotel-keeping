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

  it('kabuk MODERN gorusme alani birimi kullanir (100vh degil, dvh)', async () => {
    /*
     * Mobil tarayicilarda `100vh` adres cubugunun KAPLADIGI alani da sayar;
     * sabit ust bilgiyle birlikte bu, sayfanin altinda kirpilmis bir serit
     * ve gereksiz bir dikey kaydirma uretir. `dvh` gercek gorunur yuksekligi
     * verir ve adres cubugu gizlendikce guncellenir.
     */
    const element = await render('/de');
    const frame = element.querySelector('main#content')?.parentElement;

    expect(frame?.className).toContain('min-h-dvh');
    expect(frame?.className).not.toContain('vh]');
    expect(frame?.className).not.toContain('h-screen');
  });

  it('ust bilgi ile main KARDESTIR (sabitleme ic kaydirma kabi kurmaz)', async () => {
    /*
     * Sabitleme `position: sticky` ile yapilir; belge kaydirmasi korunur.
     * Biri kabugu "h-dvh + overflow-hidden + ic kap" desenine cevirirse
     * (panelde oldugu gibi) main artik ust bilginin kardesi olmaz — ve o
     * desenin bedeli mobil adres cubugunun hic gizlenmemesidir.
     */
    const element = await render('/de');
    // Ust bilgi bir bilesen etiketinin (hcg-guest-header) icinde durur.
    const headerHost = element.querySelector('header[data-testid="guest-header"]')?.parentElement;
    const main = element.querySelector('main#content');

    expect(headerHost?.tagName.toLowerCase()).toBe('hcg-guest-header');
    expect(headerHost?.parentElement).toBe(main?.parentElement);
    expect(headerHost?.nextElementSibling).toBe(main);

    /*
     * Sabitleme sinifi BU etikettedir — yani kapsayici blogu sayfa boyu
     * uzanan flex kabidir. Sinif icteki <header>'a tasinirsa kapsayici blok
     * bu etiketin (header yuksekliginde) kutusu olur ve yapisma araligi
     * sifira duser: hesaplanan stil dogru gorunur, cubuk yine kayip gider.
     */
    expect(headerHost?.classList.contains('hcg-header')).toBe(true);
    expect(headerHost?.parentElement?.className).toContain('flex-col');
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
