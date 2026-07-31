import { PLATFORM_ID, TransferState, makeStateKey } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { configureGuestTestBed } from '../../../testing/guest-test-bed';
import { API } from '../../../testing/public-fixtures';
import { HotelStore } from './hotel.store';
import { transferredSlot } from './transferred-slot';

/**
 * ===========================================================================
 * SUNUCU -> ISTEMCI DEVRI (alt bilgi ziplamasinin kok nedeni)
 * ===========================================================================
 *
 * Olculmus hata: prerender edilen `/de/legal/terms` masaustunde **CLS 0.60**
 * uretiyordu. Sunucu metni HTML'e basiyor, tarayici ayni metni IKINCI kez
 * cekiyor ve makaleyi yeniden ciziyordu; uzunluk degisince altindaki her sey —
 * yani alt bilgi — ziplyordu.
 *
 * Angular'in HTTP aktarim onbellegi bunu kapatmiyor (gerekce hotel.store.ts'te:
 * araci zincir sirasi). Bu dosya, yerine konan ACIK devrin iki yonunu de
 * kilitler; biri kaybolursa CLS sessizce geri gelir.
 */
/*
 * Devir icin gereken tek sey OZDESLIK; alanlarin tamami degil. Bu yuzden
 * burada sozlesme fiksturleri degil, kucuk isaretci nesneler kullanilir: bu
 * dosya "devir oldu mu" sorusunu yanitlar, "sekil dogru mu" sorusunu degil
 * (o soru sayfa testlerinin isidir).
 */
const HOTEL_FIXTURE = { slug: 'berlin-mitte', name: 'Haus Mitte' } as never;
const LEGAL_FIXTURE = {
  imprint: { legalEntityName: 'Haus Mitte GmbH' },
  documents: [],
} as never;

const HOTEL_STATE = makeStateKey<unknown>('hc.hotel');
const LEGAL_STATE = makeStateKey<unknown>('hc.legal');

describe('HotelStore — sunucuda veriyi belgeye ilistirir', () => {
  beforeEach(() =>
    configureGuestTestBed({ providers: [{ provide: PLATFORM_ID, useValue: 'server' }] }),
  );

  it('otel yaniti TransferState e yazilir', () => {
    const store = TestBed.inject(HotelStore);
    const http = TestBed.inject(HttpTestingController);

    store.load();
    http.expectOne((request) => request.url === API.hotel).flush(HOTEL_FIXTURE);

    expect(TestBed.inject(TransferState).hasKey(HOTEL_STATE)).toBe(true);
  });

  it('hukuki yanit TransferState e yazilir', () => {
    const store = TestBed.inject(HotelStore);
    const http = TestBed.inject(HttpTestingController);

    store.loadLegal();
    http.expectOne((request) => request.url === API.legal).flush(LEGAL_FIXTURE);

    expect(TestBed.inject(TransferState).hasKey(LEGAL_STATE)).toBe(true);
  });
});

describe('HotelStore — tarayicida veriyi devralir', () => {
  beforeEach(() =>
    configureGuestTestBed({ providers: [{ provide: PLATFORM_ID, useValue: 'browser' }] }),
  );

  it('devralinan hukuki metin icin IKINCI istek acilmaz', () => {
    /*
     * Testin kalbi bu: istek sayisi. Ikinci bir istek, hidrasyondan sonra
     * makalenin yeniden cizilmesi demektir — yani olculen CLS'in kendisi.
     */
    TestBed.inject(TransferState).set(LEGAL_STATE, LEGAL_FIXTURE);

    const store = TestBed.inject(HotelStore);
    const http = TestBed.inject(HttpTestingController);

    store.loadLegal();

    expect(store.legal()).toEqual(LEGAL_FIXTURE);
    expect(store.legalState().status).toBe('ready');
    http.expectNone((request) => request.url === API.legal);
  });

  it('devralinan otel kunyesi icin de istek acilmaz', () => {
    TestBed.inject(TransferState).set(HOTEL_STATE, HOTEL_FIXTURE);

    const store = TestBed.inject(HotelStore);
    const http = TestBed.inject(HttpTestingController);

    store.load();

    expect(store.hotel()).toEqual(HOTEL_FIXTURE);
    http.expectNone((request) => request.url === API.hotel);
  });

  it('devir TEK SEFERLIKTIR: "yeniden dene" gercekten yeniden ister', () => {
    /*
     * Anahtar okunduktan sonra silinmezse, hata panelindeki "yeniden dene"
     * dugmesi eski veriyi tekrar devralir ve kullaniciya hicbir sey yapmamis
     * gibi gorunur.
     */
    TestBed.inject(TransferState).set(LEGAL_STATE, LEGAL_FIXTURE);

    const store = TestBed.inject(HotelStore);
    const http = TestBed.inject(HttpTestingController);
    store.loadLegal();

    store.retryLegal();
    http.expectOne((request) => request.url === API.legal).flush(LEGAL_FIXTURE);
    expect(TestBed.inject(TransferState).hasKey(LEGAL_STATE)).toBe(false);
  });

  it('devir yoksa normal yol isler (istek acilir)', () => {
    const store = TestBed.inject(HotelStore);
    const http = TestBed.inject(HttpTestingController);

    store.loadLegal();
    http.expectOne((request) => request.url === API.legal).flush(LEGAL_FIXTURE);

    expect(store.legal()).toEqual(LEGAL_FIXTURE);
  });

  it('tarayici veriyi TEKRAR ilistirmez (belge zaten gonderildi)', () => {
    const store = TestBed.inject(HotelStore);
    const http = TestBed.inject(HttpTestingController);

    store.load();
    http.expectOne((request) => request.url === API.hotel).flush(HOTEL_FIXTURE);

    expect(TestBed.inject(TransferState).hasKey(HOTEL_STATE)).toBe(false);
  });
});

describe('transferredSlot — kapsam (scope)', () => {
  /*
   * Oda tipi detayi her slug icin BASKA bir yanittir. Kapsam anahtarin
   * parcasi olmasaydi, `/rooms/DBL` icin uretilen veri istemcide `/rooms/SUI`
   * sayfasinda devralinir ve kullaniciya yanlis oda gosterilirdi.
   */
  beforeEach(() =>
    configureGuestTestBed({ providers: [{ provide: PLATFORM_ID, useValue: 'browser' }] }),
  );

  it('yalnizca AYNI kapsamin verisi devralinir', () => {
    TestBed.inject(TransferState).set(makeStateKey<unknown>('hc.probe:DBL'), { code: 'DBL' });

    TestBed.runInInjectionContext(() => {
      const slot = transferredSlot<{ code: string }>('hc.probe');

      expect(slot.adopt('SUI'), 'baska slug devralmamali').toBe(false);
      expect(slot.adopt('DBL'), 'kendi slug u devralmali').toBe(true);
      expect(slot.data()).toEqual({ code: 'DBL' });
      expect(slot.adopt('DBL'), 'devir tek seferlik').toBe(false);
    });
  });
});

describe('transferredSlot — sunucuda devralmaz', () => {
  beforeEach(() =>
    configureGuestTestBed({ providers: [{ provide: PLATFORM_ID, useValue: 'server' }] }),
  );

  it('sunucu kendi yazdigi degeri devralmaya CALISMAZ', () => {
    /*
     * Aksi halde SSR sunucusunda `TransferState` istekler arasi paylasilan bir
     * onbellek gibi davranirdi; bu, bir misafirin diline/oteline ait yanitin
     * baska bir istege sizmasi demek olurdu.
     */
    TestBed.runInInjectionContext(() => {
      const slot = transferredSlot<{ code: string }>('hc.probe');
      slot.handOver({ code: 'DBL' }, 'DBL');

      expect(TestBed.inject(TransferState).hasKey(makeStateKey('hc.probe:DBL'))).toBe(true);
      expect(slot.adopt('DBL')).toBe(false);
    });
  });
});
