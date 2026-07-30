import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import type { AuthenticatedUser } from '../../core/models/auth.model';
import { AuthStore } from '../../core/state/auth.store';
import { HotelSwitcher } from './hotel-switcher';

function user(overrides: Partial<AuthenticatedUser> = {}): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'anna.becker@hotel.de',
    roles: ['Receptionist'],
    permissions: [],
    hotels: [
      { id: 'h-1', name: 'Hotel Adler', currency: 'EUR' },
      { id: 'h-2', name: 'Hotel Krone', currency: 'EUR' },
    ],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
    ...overrides,
  };
}

function render(session: AuthenticatedUser = user()) {
  TestBed.inject(AuthStore).setSession(session);

  const fixture = TestBed.createComponent(HotelSwitcher);
  fixture.detectChanges();

  const element = fixture.nativeElement as HTMLElement;

  return {
    fixture,
    element,
    select: element.querySelector<HTMLSelectElement>('[data-testid="hotel-switcher-select"]'),
  };
}

beforeEach(() => {
  globalThis.localStorage?.clear();

  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
    ],
  });
});

describe('HotelSwitcher — etiketsiz sunum', () => {
  it('gorunur etiket cizmez', async () => {
    const { element } = render();

    // Istek: "otel seciminde label gerekmiyor" — kontrolun icinde otel adi zaten okunur.
    expect(element.querySelector('label')).toBeNull();
    expect(element.textContent).not.toContain('hotel.label');
  });

  it('erisilebilir adi korur (`aria-label` = hotel.switcherLabel)', async () => {
    const { select } = render();

    expect(select).not.toBeNull();
    // Ceviri dosyasi birim testte yuklenmedigi icin anahtar dondurulur.
    expect(select?.getAttribute('aria-label')).toBe('hotel.switcherLabel');
    // Erisilebilir ad **bos degil**: gorunur etiket kalkarken ad kaybolmadi.
    expect(select?.getAttribute('aria-label')?.length).toBeGreaterThan(0);
  });

  it('konsolide durumu kontrolun kendi icinde tasir (ayri gosterge metnine gerek yok)', () => {
    const { element, select } = render(user({ canAccessAllHotels: true, defaultHotelId: null }));

    // Ust cubuktaki "Konsolidierte Ansicht" gostergesi kaldirildi: ayni bilgiyi
    // "Tum oteller" secenegi zaten kontrolun icinde soyluyor.
    expect(element.textContent).not.toContain('hotel.consolidated');
    expect(select?.querySelector('option')?.textContent?.trim()).toBe('hotel.allHotels');
  });
});
