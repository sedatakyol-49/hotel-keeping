import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { environment } from '../../../environments/environment';
import { configureGuestTestBed, renderRoute } from '../../../testing/guest-test-bed';
import { GuestSeoService } from './guest-seo.service';

function head(selector: string): HTMLElement[] {
  return Array.from(document.head.querySelectorAll<HTMLElement>(selector));
}

async function visit(url: string): Promise<void> {
  TestBed.inject(GuestSeoService).connect();
  await renderRoute(url);
}

beforeEach(() => {
  document.head.querySelectorAll('[data-hc-seo]').forEach((node) => node.remove());
  configureGuestTestBed({ acceptLanguage: 'de' });
});

describe('hreflang — dillerin birbirine baglanmasi', () => {
  it('uc dil + x-default alternatifi yazar', async () => {
    await visit('/de/legal/imprint');

    const alternates = head('link[rel="alternate"]').map((link) => ({
      hreflang: link.getAttribute('hreflang'),
      href: link.getAttribute('href'),
    }));

    expect(alternates).toEqual([
      ...SUPPORTED_LANGUAGES.map((language) => ({
        hreflang: language,
        href: `${environment.siteOrigin}/${language}/legal/imprint`,
      })),
      // x-default: dil pazarliginin dustugu adres.
      { hreflang: 'x-default', href: `${environment.siteOrigin}/de/legal/imprint` },
    ]);
  });

  it('karsilikliligi korur: her dil KENDISINI de bildirir', async () => {
    await visit('/tr');

    const self = head('link[rel="alternate"][hreflang="tr"]')[0];
    expect(self?.getAttribute('href')).toBe(`${environment.siteOrigin}/tr`);
  });

  it('gezinmede eski baglari birakmaz (birikme olmaz)', async () => {
    await visit('/de');
    await renderRoute('/en/search');

    expect(head('link[rel="canonical"]')).toHaveLength(1);
    expect(head('link[rel="alternate"]')).toHaveLength(SUPPORTED_LANGUAGES.length + 1);
    expect(head('link[rel="canonical"]')[0]?.getAttribute('href')).toBe(
      `${environment.siteOrigin}/en/search`,
    );
  });
});

describe('canonical ve robots', () => {
  it('kanonik adres mutlaktir ve dil on ekini tasir', async () => {
    await visit('/en');
    expect(head('link[rel="canonical"]')[0]?.getAttribute('href')).toBe(
      `${environment.siteOrigin}/en`,
    );
  });

  it('sorgu ve parca kismi kanonik adrese girmez', async () => {
    await visit('/de/search?guests=2');
    expect(head('link[rel="canonical"]')[0]?.getAttribute('href')).toBe(
      `${environment.siteOrigin}/de/search`,
    );
  });

  it('sorgu bagimli sayfayi `noindex, follow` isaretler', async () => {
    await visit('/de/search');
    expect(document.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(
      'noindex, follow',
    );
  });

  it('indekslenebilir sayfada robots etiketi BIRAKMAZ', async () => {
    await visit('/de/search');
    await renderRoute('/de');

    expect(document.querySelector('meta[name="robots"]')).toBeNull();
  });
});

describe('baslik ve aciklama', () => {
  it('rota `data` anahtarlarindan beslenir', async () => {
    await visit('/de/legal/terms');

    expect(document.title).toBe('legal.terms.meta.title');
    expect(document.querySelector('meta[name="description"]')?.getAttribute('content')).toBe(
      'legal.terms.description',
    );
  });
});
