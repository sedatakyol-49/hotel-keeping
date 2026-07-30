import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { LanguageStore } from '@hotelcore/shared';

import { configureGuestTestBed, renderRoute } from '../../../testing/guest-test-bed';

async function go(url: string): Promise<{ element: HTMLElement; router: Router }> {
  const element = await renderRoute(url);
  return { element, router: TestBed.inject(Router) };
}

describe('Dil on ekli URL cozumlemesi', () => {
  beforeEach(() => configureGuestTestBed({ acceptLanguage: 'tr-TR,tr;q=0.9,en;q=0.5' }));

  it('`/en/search` adresini `en` diliyle acar', async () => {
    const { router } = await go('/en/search');

    expect(router.url).toBe('/en/search');
    expect(TestBed.inject(LanguageStore).current()).toBe('en');
  });

  it('dil degisikliginde <html lang> niteligini gunceller', async () => {
    await go('/tr');
    expect(document.documentElement.lang).toBe('tr');

    await go('/de');
    expect(document.documentElement.lang).toBe('de');
  });

  it('derin baglantilarda da dogru dili uygular', async () => {
    await go('/tr/legal/privacy');
    expect(TestBed.inject(LanguageStore).current()).toBe('tr');
  });
});

describe('Dil pazarligi — on eksiz adresler', () => {
  beforeEach(() => configureGuestTestBed({ acceptLanguage: 'tr-TR,tr;q=0.9,en;q=0.5' }));

  it('kok adresi `Accept-Language` basligina gore yonlendirir', async () => {
    const { router } = await go('/');
    expect(router.url).toBe('/tr');
  });

  it('dil on eksiz derin baglantida yolu KAYBETMEDEN yonlendirir', async () => {
    const { router } = await go('/legal/imprint');
    expect(router.url).toBe('/tr/legal/imprint');
  });

  it('desteklenmeyen dil on ekini dogru dile cevirir (404 uretmez)', async () => {
    const { router } = await go('/fr/legal/imprint');
    expect(router.url).toBe('/tr/legal/imprint');
  });
});

describe('Dil pazarligi — baslik yoksa varsayilana duser', () => {
  beforeEach(() => configureGuestTestBed({ acceptLanguage: 'fr-FR,es;q=0.8' }));

  it('desteklenen dil yoksa `de` secilir', async () => {
    const { router } = await go('/');
    expect(router.url).toBe('/de');
  });
});

describe('Bilinmeyen sayfa', () => {
  beforeEach(() => configureGuestTestBed({ acceptLanguage: 'de' }));

  it('dil on ekinin ICINDE kalir (kabuk ve hukuki baglantilar korunur)', async () => {
    const { router, element } = await go('/de/bilinmeyen-sayfa');

    expect(router.url).toBe('/de/bilinmeyen-sayfa');
    expect(element.querySelector('[data-testid="not-found-home"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="guest-footer"]')).not.toBeNull();
  });
});
