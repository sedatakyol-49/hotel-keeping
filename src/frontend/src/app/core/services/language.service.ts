import { DOCUMENT, Injectable, effect, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { DEFAULT_LANGUAGE, normalizeLanguage, type AppLanguage } from '../models/language.model';
import { LanguageStore } from '../state/language.store';

const LANGUAGE_KEY = 'hotelcore.language';

/**
 * Dil secimi yan etkileri: ngx-translate, `<html lang|dir>` ve localStorage.
 * Oncelik sirasi: kullanici tercihi (localStorage) -> tarayici dili -> `de`.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);
  private readonly store = inject(LanguageStore);
  private readonly document = inject(DOCUMENT);

  constructor() {
    // Store degistiginde ceviri servisi ve belge nitelikleri senkron tutulur.
    effect(() => {
      const language = this.store.current();
      this.translate.use(language);
      const element = this.document.documentElement;
      element.lang = language;
      element.dir = this.store.direction();
    });
  }

  /** Uygulama acilisinda bir kez cagrilir (bkz. `app.config.ts`). */
  initialize(): AppLanguage {
    const resolved = this.resolveInitialLanguage();
    this.store.set(resolved);
    return resolved;
  }

  /** Kullanici secimi — anında uygulanir ve kalici olarak saklanir. */
  use(language: AppLanguage): void {
    this.store.set(language);
    persist(language);
  }

  private resolveInitialLanguage(): AppLanguage {
    const stored = normalizeLanguage(read());
    if (stored) {
      return stored;
    }
    const navigatorLanguages: readonly string[] = globalThis.navigator?.languages?.length
      ? globalThis.navigator.languages
      : [globalThis.navigator?.language ?? ''];
    for (const candidate of navigatorLanguages) {
      const match = normalizeLanguage(candidate);
      if (match) {
        return match;
      }
    }
    return DEFAULT_LANGUAGE;
  }
}

function read(): string | null {
  try {
    return globalThis.localStorage?.getItem(LANGUAGE_KEY) ?? null;
  } catch {
    return null;
  }
}

function persist(language: AppLanguage): void {
  try {
    globalThis.localStorage?.setItem(LANGUAGE_KEY, language);
  } catch {
    // Depolama kullanilamiyorsa secim yalnizca bu oturumda gecerli olur.
  }
}
