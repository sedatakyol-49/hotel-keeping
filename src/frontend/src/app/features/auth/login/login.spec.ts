import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { LanguageStore } from '@hotelcore/shared';
import { LoginPage } from './login';

async function render() {
  const harness = await RouterTestingHarness.create('/login');
  harness.detectChanges();

  return harness.fixture.nativeElement as HTMLElement;
}

beforeEach(() => {
  // Kalici dil tercihi testler arasinda sizmasin.
  globalThis.localStorage?.clear();

  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      provideRouter([{ path: 'login', component: LoginPage }]),
    ],
  });
});

/**
 * Dil secici ust cubuktan kaldirildi ve tek yonetim yeri Ayarlar ekrani oldu.
 * Ayarlar'a **oturum acmadan** ulasilamaz; bu yuzden giris ekranindaki secici
 * artik tek "kapi onu" caresidir: yanlis dilde acilan bir giris ekrani aksi
 * halde duzeltilemez hale gelirdi.
 */
describe('LoginPage — dil secimi', () => {
  it('giris ekraninda dil secici bulunur (Ayarlar oturum gerektirdigi icin sart)', async () => {
    const element = await render();

    expect(element.querySelector('hc-language-picker')).not.toBeNull();
  });

  it('secici header da, marka blogunun yaninda durur ve calisan dugmeler tasir', async () => {
    const element = await render();
    const picker = element.querySelector('header hc-language-picker');

    expect(picker).not.toBeNull();

    const buttons = picker?.querySelectorAll<HTMLButtonElement>('button') ?? [];
    // Desteklenen uc dil: de / en / tr.
    expect(buttons.length).toBe(3);
    expect([...buttons].map((button) => button.textContent?.trim())).toEqual(['DE', 'EN', 'TR']);
    // Dokunmatik hedef korunur.
    expect(buttons[0]?.className).toContain('min-h-touch');
  });

  it('secim giris ekranindan yapilabilir ve kalici olur', async () => {
    const element = await render();
    const buttons = [
      ...element.querySelectorAll<HTMLButtonElement>('header hc-language-picker button'),
    ];

    const turkish = buttons.find((button) => button.textContent?.trim() === 'TR');
    expect(turkish?.getAttribute('aria-pressed')).toBe('false');

    turkish?.click();

    expect(TestBed.inject(LanguageStore).current()).toBe('tr');
    expect(globalThis.localStorage?.getItem('hotelcore.language')).toBe('tr');
  });
});
