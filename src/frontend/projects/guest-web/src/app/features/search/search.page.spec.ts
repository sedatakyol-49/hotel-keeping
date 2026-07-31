import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { API, AVAILABILITY, OFFER, hold, problem } from '../../../testing/public-fixtures';
import { configureGuestTestBed, renderRoute } from '../../../testing/guest-test-bed';
import type { PublicAvailabilityResponse } from '../../core/api/public-models';

const URL = '/de/search?checkIn=2026-08-10&checkOut=2026-08-13&adults=2&children=0';

function http(): HttpTestingController {
  return TestBed.inject(HttpTestingController);
}

/** Otel kunyesi her sayfada istenir; testte sessizce yanitlanir. */
function answerHotel(): void {
  for (const request of http().match((candidate) => candidate.url === API.hotel)) {
    request.flush(null, { status: 404, statusText: 'Not Found' });
  }
}

async function open(
  response: PublicAvailabilityResponse = AVAILABILITY,
): Promise<HTMLElement> {
  const element = await renderRoute(URL);
  answerHotel();
  http().expectOne((request) => request.url === API.availability).flush(response);
  TestBed.tick();
  return element;
}

function text(element: HTMLElement, selector: string): string {
  return (element.querySelector(selector)?.textContent ?? '').replace(/\s+/gu, ' ').trim();
}

beforeEach(() => configureGuestTestBed());

describe('Arama sonuclari — PAngV', () => {
  it('kartta KDV ve Kurtaxe DAHIL toplami gosterir', async () => {
    const element = await open();

    expect(text(element, '[data-testid="price-total"]')).toContain('468,00');
    expect(text(element, '[data-testid="price-inclusive"]')).toBe('price.inclusiveVatCityTax');
  });

  it('sorguyu adresten okur ve uca aktarir', async () => {
    await renderRoute(URL);
    answerHotel();
    const request = http().expectOne((candidate) => candidate.url === API.availability);

    expect(request.request.params.get('checkIn')).toBe('2026-08-10');
    expect(request.request.params.get('checkOut')).toBe('2026-08-13');
    expect(request.request.params.get('adults')).toBe('2');
    expect(request.request.params.get('children')).toBe('0');
    request.flush(AVAILABILITY);
  });
});

describe('Arama sonuclari — UWG §5 (yaniltici kitlik yasagi)', () => {
  it('kirpilmamis ve dusuk sayida "son N oda" rozetini gosterir', async () => {
    const element = await open();
    expect(element.querySelector('[data-testid="offer-scarcity"]')).not.toBeNull();
  });

  it('sayi 5`te KIRPILMISSA kitlik iddiasi kurmaz', async () => {
    const element = await open({
      ...AVAILABILITY,
      offers: [
        {
          ...OFFER,
          availability: { isAvailable: true, availableUnits: 5, availableUnitsCapped: true },
        },
      ],
    });

    expect(element.querySelector('[data-testid="offer-scarcity"]')).toBeNull();
  });
});

describe('Arama sonuclari — bos sonuc bir hata degildir', () => {
  it('teklif yoksa hata degil, aciklamali bir bos durum gosterir', async () => {
    const element = await open({ ...AVAILABILITY, offers: [] });

    expect(element.querySelector('[data-testid="search-empty"]')).not.toBeNull();
    expect(element.querySelector('[data-error-code]')).toBeNull();
  });

  it('musait olmayan tipler icin SEBEBI gosterir (ne yapilacagi anlasilsin)', async () => {
    const element = await open({
      ...AVAILABILITY,
      offers: [],
      unavailableRoomTypes: [
        { roomTypeCode: 'SGL', name: 'Einzelzimmer', reason: 'CapacityExceeded' },
        { roomTypeCode: 'SUI', name: 'Suite', reason: 'MinNightsNotMet' },
      ],
    });

    expect(text(element, '[data-testid="unavailable-SGL"]')).toBe(
      'search.unavailable.reason.CapacityExceeded',
    );
    expect(text(element, '[data-testid="unavailable-SUI"]')).toBe(
      'search.unavailable.reason.MinNightsNotMet',
    );
  });
});

describe('Arama sonuclari — teklif secimi', () => {
  it('secim hold olusturur ve rezervasyon adimina gecer', async () => {
    const element = await open();

    element.querySelector<HTMLButtonElement>('[data-testid="offer-select-DBL"]')?.click();
    TestBed.tick();

    const request = http().expectOne(API.holds);
    expect(request.request.body).toEqual({
      roomTypeCode: 'DBL',
      checkIn: '2026-08-10',
      checkOut: '2026-08-13',
      adults: 2,
      children: 0,
    });
    request.flush(hold(), { status: 201, statusText: 'Created' });
  });

  it('son oda satildiysa ham kod yerine ne yapilacagini soyler', async () => {
    const element = await open();

    element.querySelector<HTMLButtonElement>('[data-testid="offer-select-DBL"]')?.click();
    TestBed.tick();
    http()
      .expectOne(API.holds)
      .flush(problem('ROOM_NO_LONGER_AVAILABLE'), { status: 409, statusText: 'Conflict' });
    TestBed.tick();

    expect(text(element, '[data-testid="error-body"]')).toBe(
      'errors.public.roomNoLongerAvailable',
    );
    expect(text(element, '[data-testid="error-action"]')).toBe('errors.recovery.backToSearch');
    expect(element.textContent).not.toContain('ROOM_NO_LONGER_AVAILABLE');
  });
});

describe('Arama sonuclari — sorgu yoksa', () => {
  it('tarih girilmeden istek atmaz, kullaniciyi yonlendirir', async () => {
    const element = await renderRoute('/de/search');
    answerHotel();
    TestBed.tick();

    http().expectNone((request) => request.url === API.availability);
    expect(element.querySelector('[data-testid="search-form"]')).not.toBeNull();
  });
});
