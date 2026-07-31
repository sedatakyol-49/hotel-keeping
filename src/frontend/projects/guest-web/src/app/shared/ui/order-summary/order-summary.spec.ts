import { TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { hold } from '../../../../testing/public-fixtures';
import type { PublicOrderButton, PublicOrderSummary } from '../../../core/api/public-models';
import { OrderSummary } from './order-summary';
import { resolveOrderButtonLabel } from './order-button-label';

function render(summary: PublicOrderSummary, button: PublicOrderButton) {
  const fixture = TestBed.createComponent(OrderSummary);
  fixture.componentRef.setInput('summary', summary);
  fixture.componentRef.setInput('orderButton', button);
  fixture.detectChanges();
  return fixture;
}

beforeEach(() => {
  TestBed.configureTestingModule({
    providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
  });
  /* Yalnizca parametre enterpolasyonu dogrulanacak anahtarlar (bkz. testler). */
  TestBed.inject(TranslateService).setTranslation(
    'de',
    { order: { checkTimes: 'ab {{from}} · bis {{until}}' } },
    true,
  );
});

describe('§312j Abs. 3 — Button-Losung: etiket SUNUCUDAN gelir', () => {
  it('Almanca arayuzde sunucunun `labelDe` degerini BIREBIR gosterir', () => {
    const source = hold();
    const fixture = render(source.orderSummary, source.legal.orderButton);
    const button = fixture.nativeElement.querySelector('[data-testid="order-button"]');

    expect(button?.textContent?.trim()).toBe('zahlungspflichtig buchen');
  });

  it('etiket ISTEMCIDE SABIT DEGILDIR: sunucu degeri degisince dugme de degisir', () => {
    const source = hold();
    const fixture = render(source.orderSummary, {
      ...source.legal.orderButton,
      labelDe: 'kostenpflichtig bestellen',
    });
    const button = fixture.nativeElement.querySelector('[data-testid="order-button"]');

    // Metin kodda gomulu olsaydi bu test kirilirdi.
    expect(button?.textContent?.trim()).toBe('kostenpflichtig bestellen');
  });

  it('dugmeye basildiginda GOSTERILEN metni yayar (kanit kaydi)', () => {
    const source = hold();
    const fixture = render(source.orderSummary, source.legal.orderButton);

    let emitted: string | null = null;
    fixture.componentInstance.confirm.subscribe((label: string) => (emitted = label));

    const element: HTMLElement = fixture.nativeElement;
    element.querySelector<HTMLButtonElement>('[data-testid="order-button"]')?.click();

    expect(emitted).toBe('zahlungspflichtig buchen');
  });
});

describe('§312j Abs. 3 — diger dillerdeki karsilik', () => {
  it('ceviri varsa yerellestirilmis (ama yine ucretli oldugunu bildiren) metni kullanir', () => {
    const source = hold();
    const catalog: Record<string, string> = {
      'legal.orderButton.payable': 'Book with obligation to pay',
    };

    const label = resolveOrderButtonLabel(
      source.legal.orderButton,
      'en',
      (key) => catalog[key] ?? key,
    );

    expect(label).toBe('Book with obligation to pay');
  });

  it('ceviri yoksa Almanca hukuki ifadeye duser (uydurma metin uretmez)', () => {
    const source = hold();
    const label = resolveOrderButtonLabel(source.legal.orderButton, 'tr', (key) => key);

    expect(label).toBe('zahlungspflichtig buchen');
  });
});

describe('§312j Abs. 2 — dugmenin HEMEN USTUNDEKI zorunlu ozet', () => {
  it('temel ozellikleri, sureyi ve toplam fiyati eksiksiz gosterir', () => {
    const source = hold();
    const element: HTMLElement = render(source.orderSummary, source.legal.orderButton)
      .nativeElement;

    expect(element.querySelector('[data-testid="order-room-type"]')?.textContent).toContain(
      'Doppelzimmer',
    );
    expect(element.querySelector('[data-testid="order-occupancy"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="order-board"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="order-duration"]')).not.toBeNull();
    // Yerel giris/cikis saatleri de "sure" bilgisinin parcasidir.
    expect(element.querySelector('[data-testid="order-times"]')?.textContent).toContain('15:00');
    expect(element.querySelector('[data-testid="order-total"]')?.textContent).toContain('468');
  });

  it('sunucunun verdigi HER kalemi basar — istemci bir kalemi atlayamaz', () => {
    const source = hold();
    const element: HTMLElement = render(source.orderSummary, source.legal.orderButton)
      .nativeElement;

    const rows = element.querySelectorAll('[data-testid="order-component"]');
    expect(rows).toHaveLength(source.orderSummary.components.length);

    const kinds = Array.from(rows).map((row) => row.getAttribute('data-kind'));
    expect(kinds).toEqual(['Accommodation', 'CityTax']);
    // Kurtaxe satiri gorunur ve tutari yazili (PAngV zorunlu kalem).
    expect(element.textContent).toContain('18,00');
  });

  it('toplamin KDV ve zorunlu kalemler dahil oldugunu ACIKCA beyan eder', () => {
    const source = hold();
    const element: HTMLElement = render(source.orderSummary, source.legal.orderButton)
      .nativeElement;

    expect(element.querySelector('[data-testid="order-total-note"]')?.textContent?.trim()).toBe(
      'order.totalNoteVatAndMandatory',
    );
  });

  it('DUGME, ozetin HEMEN ARDINDAN gelen kardes ogedir', () => {
    const source = hold();
    const element: HTMLElement = render(source.orderSummary, source.legal.orderButton)
      .nativeElement;

    const summary = element.querySelector('[data-testid="order-summary"]');
    const next = summary?.nextElementSibling;

    expect(next?.getAttribute('data-testid')).toBe('order-button');
  });

  it('ozet ACILIR/KAPANIR degildir (gizlenebilen bilgi "anlasilir" sayilmaz)', () => {
    const source = hold();
    const element: HTMLElement = render(source.orderSummary, source.legal.orderButton)
      .nativeElement;

    expect(element.querySelector('details')).toBeNull();
    expect(element.querySelector('[hidden]')).toBeNull();
  });

  it('gosterilen ozetin hash degeri DOM uzerinde izlenebilir', () => {
    const source = hold();
    const element: HTMLElement = render(source.orderSummary, source.legal.orderButton)
      .nativeElement;

    expect(
      element.querySelector('[data-testid="order-summary"]')?.getAttribute('data-summary-hash'),
    ).toBe(source.orderSummary.hash);
  });
});
