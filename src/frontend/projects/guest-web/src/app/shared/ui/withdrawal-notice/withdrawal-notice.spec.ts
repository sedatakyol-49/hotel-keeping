import { TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import type { PublicWithdrawalRight } from '../../../core/api/public-models';
import { WithdrawalNotice } from './withdrawal-notice';

const EXCLUDED: PublicWithdrawalRight = {
  applies: false,
  legalBasis: 'BGB §312g Abs. 2 Nr. 9',
  noticeKey: 'legal.withdrawal.excluded.accommodation',
  noticeVersion: '2026-07-01',
};

function render(right: PublicWithdrawalRight): HTMLElement {
  const fixture = TestBed.createComponent(WithdrawalNotice);
  fixture.componentRef.setInput('right', right);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

beforeEach(() => {
  TestBed.configureTestingModule({
    providers: [provideTranslateService({ lang: 'de', fallbackLang: 'de' })],
  });
  /* Yasal dayanagin ve versiyonun GERCEKTEN basildigini dogrulamak icin. */
  TestBed.inject(TranslateService).setTranslation(
    'de',
    { legal: { withdrawal: { basis: 'Rechtsgrundlage: {{basis}}', version: '{{version}}' } } },
    true,
  );
});

describe('§312g Abs. 2 Nr. 9 — cayma hakki YOK, ama bildirilmeli', () => {
  it('`applies:false` iken ISTISNA dalini gosterir', () => {
    const element = render(EXCLUDED);

    expect(element.querySelector('[data-testid="withdrawal-excluded"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="withdrawal-applies"]')).toBeNull();
  });

  it('genel Widerrufsbelehrung metnini GOSTERMEZ (var olmayan hak anlatilmaz)', () => {
    const element = render(EXCLUDED);
    const body = element.querySelector('[data-testid="withdrawal-body"]')?.textContent?.trim();

    expect(body).toBe('legal.withdrawal.excluded.accommodation');
    expect(body).not.toBe('legal.withdrawal.included.body');
  });

  it('metin anahtari SUNUCUDAN gelir (`noticeKey`), istemcide secilmez', () => {
    const element = render({ ...EXCLUDED, noticeKey: 'legal.withdrawal.custom.house' });

    expect(element.querySelector('[data-testid="withdrawal-body"]')?.textContent?.trim()).toBe(
      'legal.withdrawal.custom.house',
    );
  });

  it('yasal dayanagi ve bildirim versiyonunu gosterir (rizanin kaydi icin)', () => {
    const element = render(EXCLUDED);

    expect(element.querySelector('[data-testid="withdrawal-basis"]')?.textContent).toContain(
      'BGB §312g Abs. 2 Nr. 9',
    );
    expect(element.querySelector('[data-testid="withdrawal-version"]')?.textContent).toContain(
      '2026-07-01',
    );
  });

  it('sozlesmesel iptal hakkinin AYRI oldugunu soyler', () => {
    const element = render(EXCLUDED);
    expect(element.querySelector('[data-testid="withdrawal-cancellation-hint"]')).not.toBeNull();
  });

  it('`applies:true` gelirse (bu fazda uretilmez) DOGRU dala gecer', () => {
    const element = render({
      applies: true,
      legalBasis: 'BGB §355',
      noticeKey: 'legal.withdrawal.included.body',
      noticeVersion: '2026-07-01',
    });

    expect(element.querySelector('[data-testid="withdrawal-applies"]')).not.toBeNull();
    // Istisnaya ozgu "iptal ayridir" notu bu dalda gosterilmez.
    expect(element.querySelector('[data-testid="withdrawal-cancellation-hint"]')).toBeNull();
  });
});
