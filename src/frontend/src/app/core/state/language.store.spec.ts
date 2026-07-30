import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { LanguageStore } from '@hotelcore/shared';

describe('LanguageStore', () => {
  let store: LanguageStore;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    store = TestBed.inject(LanguageStore);
  });

  it('varsayilan dil Almancadir', () => {
    expect(store.current()).toBe('de');
    expect(store.locale()).toBe('de-DE');
    expect(store.direction()).toBe('ltr');
    expect(store.acceptLanguageHeader()).toBe('de');
  });

  it('dil degisince locale ve Accept-Language turetilir', () => {
    store.set('tr');
    expect(store.locale()).toBe('tr-TR');
    expect(store.acceptLanguageHeader()).toBe('tr');

    store.set('en');
    expect(store.locale()).toBe('en-GB');
  });

  it('desteklenen dilleri yayinlar', () => {
    expect([...store.available]).toEqual(['de', 'en', 'tr']);
  });
});
