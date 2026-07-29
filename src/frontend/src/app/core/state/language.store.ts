import { Injectable, computed, signal } from '@angular/core';

import {
  DEFAULT_LANGUAGE,
  LANGUAGE_DIRECTIONS,
  LANGUAGE_LOCALES,
  SUPPORTED_LANGUAGES,
  type AppLanguage,
} from '../models/language.model';

/**
 * Aktif dilin tek kaynagi (signal store).
 * Yan etkiler (ngx-translate, `<html lang>`, localStorage) `LanguageService`
 * tarafindan yonetilir; burasi yalnizca durumu tutar.
 */
@Injectable({ providedIn: 'root' })
export class LanguageStore {
  private readonly _current = signal<AppLanguage>(DEFAULT_LANGUAGE);

  readonly current = this._current.asReadonly();
  readonly available = SUPPORTED_LANGUAGES;
  readonly locale = computed(() => LANGUAGE_LOCALES[this._current()]);
  readonly direction = computed(() => LANGUAGE_DIRECTIONS[this._current()]);
  /** `Accept-Language` header degeri (api-contracts.md — Genel Kurallar). */
  readonly acceptLanguageHeader = computed(() => this._current());

  set(language: AppLanguage): void {
    if (this._current() !== language) {
      this._current.set(language);
    }
  }
}
