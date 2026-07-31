import { TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { PRICE } from '../../../../testing/public-fixtures';
import type { PublicPrice } from '../../../core/api/public-models';
import { PriceBlock } from './price-block';

function render(price: PublicPrice, variant: 'compact' | 'full' = 'full'): HTMLElement {
  const fixture = TestBed.createComponent(PriceBlock);
  fixture.componentRef.setInput('price', price);
  fixture.componentRef.setInput('nights', price.nightly.length);
  fixture.componentRef.setInput('variant', variant);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

/** Bosluk turleri (NBSP, dar NBSP) karsilastirmayi bozmasin. */
function text(element: Element | null): string {
  return (element?.textContent ?? '').replace(/\s+/gu, ' ').trim();
}

beforeEach(() => {
  TestBed.configureTestingModule({
    providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
  });
  /* Tutarin gercekten basildigini dogrulayabilmek icin iskelet sablonlar. */
  TestBed.inject(TranslateService).setTranslation(
    'de',
    {
      price: {
        vatNote: 'MwSt ({{rate}}): {{amount}}',
        dueAtProperty: 'bei Anreise: {{amount}}',
      },
    },
    true,
  );
});

describe('PAngV — gosterilen fiyat KDV ve zorunlu kalemler DAHIL toplamdir', () => {
  it('buyuk rakam `totalGross`tur (Kurtaxe iceride)', () => {
    const element = render(PRICE);

    // 468,00 = 450,00 konaklama + 18,00 Kurtaxe
    expect(text(element.querySelector('[data-testid="price-total"]'))).toContain('468,00');
    expect(PRICE.accommodationGross + PRICE.cityTax.amount).toBe(PRICE.totalGross);
  });

  it('kapsayicilik beyani sunucunun bayraklarindan turer', () => {
    const element = render(PRICE);

    expect(text(element.querySelector('[data-testid="price-inclusive"]'))).toBe(
      'price.inclusiveVatCityTax',
    );
  });

  it('Kurtaxe uygulanmiyorsa "Kurtaxe dahil" DEMEZ', () => {
    const element = render({
      ...PRICE,
      totalGross: 450,
      cityTax: { ...PRICE.cityTax, applies: false, amount: 0 },
    });

    expect(text(element.querySelector('[data-testid="price-inclusive"]'))).toBe(
      'price.inclusiveVat',
    );
    expect(element.querySelector('[data-testid="price-city-tax"]')).toBeNull();
  });

  it('Kurtaxe AYRI satir olarak, dayanagiyla birlikte gorunur', () => {
    const element = render(PRICE);

    expect(text(element.querySelector('[data-testid="price-city-tax"]'))).toContain('18,00');
    expect(element.querySelector('[data-testid="city-tax-basis"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="price-city-tax-note"]')).not.toBeNull();
  });

  it('cocuk muafiyeti uygulandiysa dipnotu gosterir', () => {
    const element = render(PRICE);
    expect(element.querySelector('[data-testid="price-child-exemption"]')).not.toBeNull();
  });

  it('icerideki KDV tutarini ve oranini gosterir', () => {
    const element = render(PRICE);
    expect(text(element.querySelector('[data-testid="price-vat-note"]'))).toContain('29,44');
  });

  it('girişte odenecek tutari ayrica bildirir', () => {
    const element = render(PRICE);
    expect(text(element.querySelector('[data-testid="price-due-note"]'))).toContain('468,00');
  });
});

describe('PAngV — gecelik fiyat yaniltici olamaz', () => {
  it('geceler esitse dogrudan gecelik fiyati yazar', () => {
    const element = render(PRICE);
    expect(text(element.querySelector('[data-testid="price-nightly"]'))).toContain(
      'price.perNight',
    );
  });

  it('geceler farkliysa ORTALAMA oldugunu acikca soyler', () => {
    const element = render({
      ...PRICE,
      nightly: [
        { date: '2026-08-10', gross: 120 },
        { date: '2026-08-11', gross: 150 },
        { date: '2026-08-12', gross: 180 },
      ],
    });

    expect(text(element.querySelector('[data-testid="price-nightly"]'))).toContain(
      'price.perNightAverage',
    );
  });
});

describe('Arama sonucundaki kompakt gorunum', () => {
  it('kirilim olmadan da toplami ve kapsayicilik beyanini tasir', () => {
    const element = render(PRICE, 'compact');

    expect(text(element.querySelector('[data-testid="price-total"]'))).toContain('468,00');
    expect(text(element.querySelector('[data-testid="price-inclusive"]'))).toBe(
      'price.inclusiveVatCityTax',
    );
    expect(element.querySelector('[data-testid="price-breakdown"]')).toBeNull();
  });
});
