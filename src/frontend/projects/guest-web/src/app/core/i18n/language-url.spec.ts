import { describe, expect, it } from 'vitest';

import {
  languagePath,
  negotiateLanguage,
  parseAcceptLanguage,
  splitLanguagePrefix,
  toLanguageUrl,
  withLanguage,
} from './language-url';

describe('splitLanguagePrefix — dil on eki cozumlemesi', () => {
  it('desteklenen dil on ekini ayirir ve geri kalan yolu verir', () => {
    expect(splitLanguagePrefix('/en/legal/imprint')).toEqual({
      language: 'en',
      path: 'legal/imprint',
      suffix: '',
    });
  });

  it('sorgu ve parca kismini korur (kaybolursa filtreler silinir)', () => {
    expect(splitLanguagePrefix('/de/search?from=2026-08-01#results')).toEqual({
      language: 'de',
      path: 'search',
      suffix: '?from=2026-08-01#results',
    });
  });

  it('desteklenmeyen bir on eki dil saymaz', () => {
    expect(splitLanguagePrefix('/fr/search').language).toBeNull();
  });

  it('yalnizca dil segmentinden olusan adresi bos yol olarak dondurur', () => {
    expect(splitLanguagePrefix('/tr')).toEqual({ language: 'tr', path: '', suffix: '' });
  });
});

describe('withLanguage — dil secici davranisi', () => {
  it('kullaniciyi ayni sayfada tutarak dili degistirir', () => {
    expect(withLanguage('/de/legal/privacy', 'tr')).toBe('/tr/legal/privacy');
  });

  it('ana sayfada dogru kok adresi uretir (cift egik cizgi yok)', () => {
    expect(withLanguage('/de', 'en')).toBe('/en');
  });

  it('sorgu parametrelerini tasir', () => {
    expect(withLanguage('/en/search?guests=2', 'de')).toBe('/de/search?guests=2');
  });
});

describe('toLanguageUrl — yonlendirme hedefi', () => {
  it('on eksiz adrese dil ekler', () => {
    expect(toLanguageUrl('/search', 'de')).toBe('/de/search');
  });

  it('desteklenmeyen dil on ekini atar, icerigi korur', () => {
    // `/fr/legal/imprint` -> 404 degil, ayni belge dogru dilde.
    expect(toLanguageUrl('/fr/legal/imprint', 'de')).toBe('/de/legal/imprint');
  });

  it('kok adres icin yalnizca dili dondurur', () => {
    expect(toLanguageUrl('/', 'tr')).toBe('/tr');
  });
});

describe('languagePath', () => {
  it('dil on ekli mutlak yol uretir', () => {
    expect(languagePath('tr', 'legal', 'imprint')).toBe('/tr/legal/imprint');
    expect(languagePath('de')).toBe('/de');
  });
});

describe('parseAcceptLanguage / negotiateLanguage — dil pazarligi', () => {
  it('kalite degerine gore siralar', () => {
    expect(parseAcceptLanguage('en;q=0.8,de-DE,tr;q=0.9')).toEqual(['de-DE', 'tr', 'en']);
  });

  it('q=0 olan dili eler', () => {
    expect(parseAcceptLanguage('fr;q=0,en')).toEqual(['en']);
  });

  it('bos/gecersiz basligi bos listeye indirger', () => {
    expect(parseAcceptLanguage(null)).toEqual([]);
    expect(parseAcceptLanguage('')).toEqual([]);
  });

  it('ilk desteklenen dili secer (bolgesel etiketi indirger)', () => {
    expect(negotiateLanguage(['fr-FR', 'tr-TR', 'en'])).toBe('tr');
  });

  it('hicbiri desteklenmiyorsa varsayilan dile duser', () => {
    expect(negotiateLanguage(['fr', 'es'])).toBe('de');
    expect(negotiateLanguage([])).toBe('de');
  });
});
