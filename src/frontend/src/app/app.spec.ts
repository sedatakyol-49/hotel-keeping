import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TitleStrategy } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { LanguageStore, SUPPORTED_LANGUAGES } from '@hotelcore/shared';

import { App } from './app';
import { appConfig } from './app.config';
import { AuthService } from './core/services/auth.service';
import { CurrentHotelService } from './core/services/current-hotel.service';
import { LanguageService } from './core/services/language.service';
import { TokenStorageService } from './core/services/token-storage.service';
import { TranslatedTitleStrategy } from './core/services/translated-title.strategy';
import { AuthStore } from './core/state/auth.store';

/**
 * Duman testi: `appConfig` saglayici grafiginin cozuldugunu dogrular.
 * Interceptor -> servis -> store zincirinde dairesel bagimlilik olusursa
 * (NG0200) bu test kirilir.
 */
describe('App bootstrap', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [...appConfig.providers, provideHttpClientTesting()],
    });
  });

  it('kok bileseni olusturur', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('cekirdek servisleri ve store lari cozer', () => {
    expect(TestBed.inject(AuthStore)).toBeTruthy();
    expect(TestBed.inject(LanguageStore)).toBeTruthy();
    expect(TestBed.inject(AuthService)).toBeTruthy();
    expect(TestBed.inject(LanguageService)).toBeTruthy();
    expect(TestBed.inject(TokenStorageService)).toBeTruthy();
    expect(TestBed.inject(CurrentHotelService)).toBeTruthy();
    expect(TestBed.inject(TitleStrategy)).toBeInstanceOf(TranslatedTitleStrategy);
  });

  it('acilista desteklenen bir dil secer', () => {
    const language = TestBed.inject(LanguageStore).current();
    expect(SUPPORTED_LANGUAGES).toContain(language);
  });

  it('oturum acilmadan once anonimdir', () => {
    expect(TestBed.inject(AuthStore).isAuthenticated()).toBe(false);
  });
});
