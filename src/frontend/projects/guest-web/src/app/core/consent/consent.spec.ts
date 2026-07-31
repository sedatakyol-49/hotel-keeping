import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { ConsentStore } from './consent.store';
import { TrackerService } from './tracker.service';

/** Testte gercek `localStorage` yerine denetlenebilir bir depo. */
function memoryStorage(): Storage {
  const map = new Map<string, string>();
  return {
    get length() {
      return map.size;
    },
    clear: () => map.clear(),
    getItem: (key: string) => map.get(key) ?? null,
    key: (index: number) => Array.from(map.keys())[index] ?? null,
    removeItem: (key: string) => map.delete(key),
    setItem: (key: string, value: string) => map.set(key, value),
  } as Storage;
}

beforeEach(() => {
  TestBed.configureTestingModule({});
  document.head.querySelectorAll('[data-hc-tracker]').forEach((node) => node.remove());
});

describe('§25 TDDDG — baslangic durumu: onay YOK', () => {
  it('karar verilmemis durumda baslar (on isaretli kutu yoktur)', () => {
    const store = TestBed.inject(ConsentStore);

    expect(store.decision()).toBe('unknown');
    expect(store.analyticsAllowed()).toBe(false);
    expect(store.bannerVisible()).toBe(true);
  });

  it('reddetmek kabul etmek kadar kolaydir: ikisi de TEK cagri, ayni etki alani', () => {
    const store = TestBed.inject(ConsentStore);
    const storage = memoryStorage();

    store.decline(storage);
    expect(store.decision()).toBe('denied');
    expect(store.analyticsAllowed()).toBe(false);
    expect(store.bannerVisible()).toBe(false);

    // Karar kalicidir; ikisi de ayni bicimde saklanir.
    expect(storage.getItem('hc.tdddg.consent.v1')).toContain('denied');
  });

  it('kabul edilen karar da saklanir ve izleyiciye izin verir', () => {
    const store = TestBed.inject(ConsentStore);
    const storage = memoryStorage();

    store.accept(storage);
    expect(store.analyticsAllowed()).toBe(true);
    expect(storage.getItem('hc.tdddg.consent.v1')).toContain('granted');
  });

  it('kaydedilmis karar geri yuklenir', () => {
    const storage = memoryStorage();
    storage.setItem(
      'hc.tdddg.consent.v1',
      JSON.stringify({ decision: 'granted', decidedAt: '2026-07-31T00:00:00Z', version: 1 }),
    );

    const store = TestBed.inject(ConsentStore);
    store.restore(storage);

    expect(store.decision()).toBe('granted');
    expect(store.bannerVisible()).toBe(false);
  });

  it('bozuk kayit "onay verilmemis" sayilir (guvenli varsayilan)', () => {
    const storage = memoryStorage();
    storage.setItem('hc.tdddg.consent.v1', '{bozuk');

    const store = TestBed.inject(ConsentStore);
    store.restore(storage);

    expect(store.decision()).toBe('unknown');
    expect(store.analyticsAllowed()).toBe(false);
  });

  it('karar geri alinabilir: bant yeniden acilir', () => {
    const store = TestBed.inject(ConsentStore);
    store.accept(memoryStorage());
    expect(store.bannerVisible()).toBe(false);

    store.reopen();
    expect(store.bannerVisible()).toBe(true);
  });
});

describe('§25 TDDDG — onaysiz hicbir izleyici DOM\'a girmez', () => {
  it('karar verilmeden once script eklenmez', () => {
    const tracker = TestBed.inject(TrackerService);
    tracker.connect();
    TestBed.tick();

    expect(document.querySelectorAll('[data-hc-tracker]')).toHaveLength(0);
  });

  it('reddedildikten sonra da script eklenmez', () => {
    const store = TestBed.inject(ConsentStore);
    const tracker = TestBed.inject(TrackerService);
    tracker.connect();

    store.decline(memoryStorage());
    TestBed.tick();

    expect(document.querySelectorAll('[data-hc-tracker]')).toHaveLength(0);
  });

  it('onay geri alindiginda eklenmis etiketler KALDIRILIR', () => {
    const store = TestBed.inject(ConsentStore);
    const tracker = TestBed.inject(TrackerService);
    tracker.connect();

    // Onay verilmis gibi bir etiket yerlestir (saglayici eklendiginde olusacak durum).
    store.accept(memoryStorage());
    TestBed.tick();
    const script = document.createElement('script');
    script.setAttribute('data-hc-tracker', '');
    document.head.appendChild(script);

    store.decline(memoryStorage());
    TestBed.tick();

    expect(document.querySelectorAll('[data-hc-tracker]')).toHaveLength(0);
  });

  it('bu fazda kayitli bir izleyici yoktur — kural yine de zorlanir', () => {
    expect(TrackerService.trackerCount).toBe(0);
  });
});
