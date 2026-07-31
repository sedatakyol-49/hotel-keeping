import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { REQUEST } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { LEGAL_SNAPSHOT, type LegalSnapshot } from '../legal/legal-snapshot';
import { legalPrerenderInterceptor } from './public-http.interceptors';
import type { PublicLegalResponse } from './public-models';

/**
 * ===========================================================================
 * §5 DDG — PRERENDER EDILEN IMPRESSUM BOS KALMAZ
 * ===========================================================================
 *
 * Hukuki sayfalar derleme aninda uretilir. O anda gelen bir HTTP istegi yoktur,
 * dolayisiyla `GET /legal` cagrisi normalde duser ve uretilen HTML'de kunye
 * gorunmez — JavaScript calistirmayan bir ziyaretci Impressum'u hic goremezdi.
 * Bu testler o kapinin kapali kalmasini saglar:
 *
 *  1) gelen istek YOKSA (prerender) yanit anlik goruntuden gelir,
 *  2) gelen istek VARSA (SSR) canli API kullanilir — metin bayatlamaz,
 *  3) hukuki olmayan adresler hicbir zaman anlik goruntuden yanitlanmaz.
 */
const IMPRINT: PublicLegalResponse = {
  imprint: {
    legalEntityName: 'HotelCore Berlin Betriebs GmbH',
    legalForm: 'GmbH',
    representedBy: 'Anna Becker',
    addressLine: 'Chausseestrasse 5',
    postalCode: '10115',
    city: 'Berlin',
    country: 'DE',
    phone: '+49 30 1234567',
    email: 'info@hotelcore.local',
    registerCourt: 'Amtsgericht Berlin-Charlottenburg',
    registerNumber: 'HRB 284913 B',
    vatId: 'DE289176543',
    supervisoryAuthority: null,
    disputeResolution: {
      participatesInAdr: false,
      noticeKey: 'legal.adr.notParticipating',
      odrPlatformUrl: 'https://ec.europa.eu/consumers/odr/',
    },
  },
  documents: [
    { key: 'terms', title: 'AGB', version: '2026-07-01', culture: 'de', bodyHtml: '<p>AGB</p>' },
  ],
};

const SNAPSHOT: LegalSnapshot = {
  hotelSlug: 'berlin-mitte',
  generatedAt: '2026-07-31T00:00:00.000Z',
  documents: { de: IMPRINT },
};

function configure(options: { snapshot?: LegalSnapshot | null; incomingRequest?: boolean } = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([legalPrerenderInterceptor])),
      provideHttpClientTesting(),
      { provide: LEGAL_SNAPSHOT, useValue: options.snapshot ?? null },
      ...(options.incomingRequest === true
        ? [{ provide: REQUEST, useValue: { url: 'http://localhost/de/legal/imprint' } as Request }]
        : []),
    ],
  });
}

describe('legalPrerenderInterceptor', () => {
  it('prerender sirasinda /legal yanitini anlik goruntuden verir (ag istegi YOK)', async () => {
    configure({ snapshot: SNAPSHOT });

    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);

    const response = await new Promise<PublicLegalResponse>((resolve, reject) => {
      http
        .get<PublicLegalResponse>('/api/v1/public/hotels/berlin-mitte/legal', {
          headers: { 'Accept-Language': 'de' },
        })
        .subscribe({ next: resolve, error: reject });
    });

    expect(response.imprint.legalEntityName).toBe('HotelCore Berlin Betriebs GmbH');
    // Anlik goruntu kullanildiginda ag katmanina hic inilmez.
    controller.expectNone('/api/v1/public/hotels/berlin-mitte/legal');
  });

  it('gercek bir istek baglami varsa (SSR) canli API kullanilir', () => {
    configure({ snapshot: SNAPSHOT, incomingRequest: true });

    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);

    http.get('/api/v1/public/hotels/berlin-mitte/legal').subscribe();

    controller.expectOne('/api/v1/public/hotels/berlin-mitte/legal');
    controller.verify();
  });

  it('anlik goruntu yoksa istek oldugu gibi gecer', () => {
    configure({ snapshot: null });

    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);

    http.get('/api/v1/public/hotels/berlin-mitte/legal').subscribe();

    controller.expectOne('/api/v1/public/hotels/berlin-mitte/legal');
    controller.verify();
  });

  it('hukuki olmayan adresleri asla anlik goruntuden yanitlamaz', () => {
    configure({ snapshot: SNAPSHOT });

    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);

    http.get('/api/v1/public/hotels/berlin-mitte/availability').subscribe();

    controller.expectOne('/api/v1/public/hotels/berlin-mitte/availability');
    controller.verify();
  });
});
