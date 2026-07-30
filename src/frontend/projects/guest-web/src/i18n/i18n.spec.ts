import { describe, expect, it } from 'vitest';

import { DEFAULT_LANGUAGE, SUPPORTED_LANGUAGES, type AppLanguage } from '@hotelcore/shared';

import de from './de.json';
import en from './en.json';
import tr from './tr.json';

const BUNDLES: Record<AppLanguage, unknown> = { de, en, tr };

function keysOf(value: unknown, prefix = ''): string[] {
  if (value === null || typeof value !== 'object') {
    return [prefix];
  }
  return Object.entries(value as Record<string, unknown>).flatMap(([key, child]) =>
    keysOf(child, prefix ? `${prefix}.${key}` : key),
  );
}

describe('Ceviri paketleri', () => {
  it('desteklenen her dil icin bir paket vardir', () => {
    expect(Object.keys(BUNDLES).sort()).toEqual([...SUPPORTED_LANGUAGES].sort());
  });

  it('tum diller ayni anahtar kumesini tasir (eksik ceviri = gorunur anahtar)', () => {
    const reference = keysOf(BUNDLES[DEFAULT_LANGUAGE]).sort();

    for (const language of SUPPORTED_LANGUAGES) {
      const keys = keysOf(BUNDLES[language]).sort();
      const missing = reference.filter((key) => !keys.includes(key));
      const extra = keys.filter((key) => !reference.includes(key));

      expect(missing, `${language} icinde eksik`).toEqual([]);
      expect(extra, `${language} icinde fazla`).toEqual([]);
    }
  });

  it('hicbir deger bos degildir', () => {
    for (const language of SUPPORTED_LANGUAGES) {
      const flat = flatten(BUNDLES[language]);
      const empty = Object.entries(flat)
        .filter(([, value]) => value.trim().length === 0)
        .map(([key]) => key);

      expect(empty, `${language} icinde bos deger`).toEqual([]);
    }
  });

  it('hukuki sayfalarin zorunlu bolum basliklari uc dilde de vardir', () => {
    for (const language of SUPPORTED_LANGUAGES) {
      const flat = flatten(BUNDLES[language]);
      for (const slug of ['imprint', 'privacy', 'terms']) {
        expect(flat[`legal.${slug}.label`], `${language}/${slug} etiketi`).toBeTypeOf('string');
        expect(flat[`legal.${slug}.meta.title`], `${language}/${slug} basligi`).toBeTypeOf(
          'string',
        );
      }
    }
  });
});

function flatten(value: unknown, prefix = '', target: Record<string, string> = {}) {
  if (value === null || typeof value !== 'object') {
    target[prefix] = String(value);
    return target;
  }
  for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
    flatten(child, prefix ? `${prefix}.${key}` : key, target);
  }
  return target;
}
