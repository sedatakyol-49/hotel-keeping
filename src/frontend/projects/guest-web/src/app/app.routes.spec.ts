import type { Route } from '@angular/router';
import { RenderMode } from '@angular/ssr';
import { describe, expect, it } from 'vitest';

import { SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { LANGUAGE_ROUTES, routes } from './app.routes';
import { serverRoutes } from './app.routes.server';
import { LEGAL_DOCUMENTS } from './features/legal/legal-documents';

const languageRoute = routes.find((route) => route.path === ':lang') as Route;

/** `status` alani ServerRoute birlesiminin yalnizca bazi uyelerinde bulunur. */
function statusOf(path: string): number | undefined {
  const route = serverRoutes.find((candidate) => candidate.path === path);
  return route !== undefined && 'status' in route ? route.status : undefined;
}

describe('Rota iskeleti — kok tablo', () => {
  it('yalnizca uc dal icerir: kok, :lang ve yakalayici', () => {
    expect(routes.map((route) => route.path)).toEqual(['', ':lang', '**']);
  });

  it('dil dali `canMatch` ile korunur (eslesmezse yakalayiciya duser)', () => {
    // canActivate ile korunsaydi `/fr/...` 404 olurdu; canMatch ile
    // yonlendirilebilir hale gelir.
    expect(languageRoute.canMatch).toHaveLength(1);
    expect(languageRoute.canActivate).toHaveLength(1);
  });

  it('dil on eksiz her adres bir guard tarafindan yakalanir', () => {
    for (const path of ['', '**']) {
      const route = routes.find((candidate) => candidate.path === path) as Route;
      expect(route.canActivate, `"${path}" korumasiz`).toHaveLength(1);
    }
  });
});

describe('Rota iskeleti — dil altindaki sayfalar', () => {
  const paths = LANGUAGE_ROUTES.map((route) => route.path);

  it('bu turda planlanan tum ekranlari tasir', () => {
    expect(paths).toEqual([
      '',
      'search',
      'rooms/:slug',
      'booking',
      'confirmation/:reference',
      'legal/imprint',
      'legal/privacy',
      'legal/terms',
      '**',
    ]);
  });

  it('hukuki rotalar tek kaynak listeden uretilir', () => {
    const legalPaths = paths.filter((path) => path?.startsWith('legal/'));
    expect(legalPaths).toEqual(LEGAL_DOCUMENTS.map((document) => `legal/${document.slug}`));
  });

  it('her sayfa SEO sozlesmesini (`titleKey`) bildirir', () => {
    for (const route of LANGUAGE_ROUTES) {
      expect(route.data?.['titleKey'], `"${route.path}" basliksiz`).toBeTypeOf('string');
    }
  });

  it('kisiye ozel ve sorgu bagimli sayfalar dizine eklenmez', () => {
    const noindex = LANGUAGE_ROUTES.filter((route) => route.data?.['noindex'] === true).map(
      (route) => route.path,
    );
    expect(noindex).toEqual(['search', 'booking', 'confirmation/:reference', '**']);
  });

  it('tum sayfalar lazy yuklenir (ilk paket sayfa kodu tasimaz)', () => {
    for (const route of LANGUAGE_ROUTES) {
      expect(route.loadComponent, `"${route.path}" eager`).toBeTypeOf('function');
    }
  });
});

describe('Sunucu rota tablosu — render modlari', () => {
  it('istemci rotalarinin her biri icin bir render modu tanimlar', () => {
    const serverPaths = serverRoutes.map((route) => route.path);
    expect(serverPaths).toContain('');
    expect(serverPaths).toContain(':lang');
    expect(serverPaths).toContain('**');

    for (const document of LEGAL_DOCUMENTS) {
      expect(serverPaths).toContain(`:lang/legal/${document.slug}`);
    }
  });

  it('dil icindeki bilinmeyen adres gercek 404 dondurur (soft 404 degil)', () => {
    expect(statusOf(':lang/**')).toBe(404);

    // Dil on eksiz adres yonlendirilir; ona 404 damgalanmamalidir.
    expect(statusOf('**')).toBeUndefined();
  });

  it('kisiye ozel akislar sunucuda render EDILMEZ (istemci modu)', () => {
    const clientOnly = serverRoutes
      .filter((route) => route.renderMode === RenderMode.Client)
      .map((route) => route.path);

    expect(clientOnly).toEqual([':lang/booking', ':lang/confirmation/:reference']);
  });

  it('prerender edilen rotalar uc dilin tamamini uretir', async () => {
    const prerendered = serverRoutes.filter((route) => 'getPrerenderParams' in route);
    expect(prerendered.length).toBeGreaterThan(0);

    for (const route of prerendered) {
      const params = await (
        route as { getPrerenderParams: () => Promise<Record<string, string>[]> }
      ).getPrerenderParams();
      expect(params.map((entry) => entry['lang'])).toEqual([...SUPPORTED_LANGUAGES]);
    }
  });
});
