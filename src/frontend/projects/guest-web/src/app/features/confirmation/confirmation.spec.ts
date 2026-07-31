import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { API, booking } from '../../../testing/public-fixtures';
import {
  configureGuestTestBed,
  renderRoute,
  useTestTranslations,
} from '../../../testing/guest-test-bed';

const TOKEN = 'hQ7pR2vK9mNc4XsA1TjW6bYdZ0f';

function http(): HttpTestingController {
  return TestBed.inject(HttpTestingController);
}

function text(element: HTMLElement, selector: string): string {
  return (element.querySelector(selector)?.textContent ?? '').replace(/\s+/gu, ' ').trim();
}

beforeEach(() => {
  configureGuestTestBed();
  /* Mutlak son tarihin gercekten basildigini gorebilmek icin. */
  useTestTranslations({
    booking: {
      cancellation: { until: 'frei bis {{deadline}}' },
      legal: { orderButton: 'Bestellschaltflaeche: {{label}}' },
    },
    legal: { withdrawal: { basis: 'Rechtsgrundlage: {{basis}}' } },
  });
});

describe('Onay ekrani — §312f (kalici veri tasiyicisi)', () => {
  it('onayin E-POSTA ile gonderildigini acikca bildirir', async () => {
    const element = await renderRoute(`/de/confirmation/${TOKEN}`);
    http().expectOne(API.booking(TOKEN)).flush(booking());
    TestBed.tick();

    expect(text(element, '[data-testid="confirmation-email"]')).toContain(
      'confirmation.emailBody',
    );
    expect(element.querySelector('[data-testid="confirmation-document-version"]')).not.toBeNull();
  });

  it('rezervasyon numarasini gosterir (referans, token degil)', async () => {
    const element = await renderRoute(`/de/confirmation/${TOKEN}`);
    http().expectOne(API.booking(TOKEN)).flush(booking());
    TestBed.tick();

    expect(text(element, '[data-testid="booking-reference"]')).toBe('K7QM-3XPD-9RTV');
  });

  it('fiyati KDV ve Kurtaxe dahil, kirilimiyla birlikte tekrar gosterir', async () => {
    const element = await renderRoute(`/de/confirmation/${TOKEN}`);
    http().expectOne(API.booking(TOKEN)).flush(booking());
    TestBed.tick();

    expect(text(element, '[data-testid="price-total"]')).toContain('468,00');
    expect(text(element, '[data-testid="price-city-tax"]')).toContain('18,00');
  });

  it('cayma hakki bildiriminin DONMUS kopyasini tasir', async () => {
    const element = await renderRoute(`/de/confirmation/${TOKEN}`);
    http().expectOne(API.booking(TOKEN)).flush(booking());
    TestBed.tick();

    expect(element.querySelector('[data-testid="withdrawal-excluded"]')).not.toBeNull();
    expect(text(element, '[data-testid="withdrawal-basis"]')).toContain(
      'BGB §312g Abs. 2 Nr. 9',
    );
  });

  it('gosterilen siparis dugmesi metnini kanit olarak yeniden gosterir', async () => {
    const element = await renderRoute(`/de/confirmation/${TOKEN}`);
    http().expectOne(API.booking(TOKEN)).flush(booking());
    TestBed.tick();

    expect(text(element, '[data-testid="legal-button-label"]')).toContain(
      'zahlungspflichtig buchen',
    );
  });

  it('ucretsiz iptal son tarihini MUTLAK bir an olarak gosterir', async () => {
    const element = await renderRoute(`/de/confirmation/${TOKEN}`);
    http().expectOne(API.booking(TOKEN)).flush(booking());
    TestBed.tick();

    const deadline = text(element, '[data-testid="cancellation-deadline"]');
    expect(deadline).toContain('frei bis');
    expect(deadline).toContain('2026');
  });
});
