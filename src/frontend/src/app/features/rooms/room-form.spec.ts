import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { RoomResponse } from '../../core/models/room.model';
import type { RoomTypeResponse } from '../../core/models/room-type.model';
import { RoomFormPage } from './room-form';
import { RoomTypeFormPage } from './room-type-form';

const ROOM_TYPE: RoomTypeResponse = {
  id: 't-1',
  code: 'DBL',
  name: 'Doppelzimmer',
  basePrice: 129,
  currency: 'EUR',
  capacity: 2,
  amenities: ['wifi'],
  roomCount: 4,
};

const CREATED_ROOM: RoomResponse = {
  id: 'r-1',
  number: '201',
  floor: 2,
  roomTypeId: 't-1',
  roomTypeCode: 'DBL',
  roomTypeName: 'Doppelzimmer',
  housekeepingStatus: 'Clean',
  isOutOfOrder: false,
  note: null,
};

/**
 * Uygulama **zoneless** oldugu icin `whenStable()` bekleyen promise'leri
 * beklemez; makrogorev sirasina gecmek gerekir.
 */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function setValue(element: HTMLElement, selector: string, value: string): void {
  const input = element.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
  input!.value = value;
  input!.dispatchEvent(new Event('input'));
  input!.dispatchEvent(new Event('change'));
}

describe('RoomFormPage — submit akisi', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  /** Olusturma modunda ekrani canlandirir ve oda tipi listesini karsilar. */
  async function render(): Promise<ComponentFixture<RoomFormPage>> {
    const fixture = TestBed.createComponent(RoomFormPage);
    fixture.detectChanges();

    http.expectOne((request) => request.url === `${baseUrl}/room-types`).flush([ROOM_TYPE]);
    await tick();
    fixture.detectChanges();
    return fixture;
  }

  it('formu POST /rooms govdesine cevirir: floor **sayi** olarak gider', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');

    setValue(element, '#room-number', '201');
    // `<input type="number">` bagli kontrole Angular **sayi** yazar; donusum
    // bu yuzden `parseInteger` uzerinden gecer (sayi girdisi de kabul edilir).
    setValue(element, '#room-floor', '2');
    setValue(element, '#room-type', 't-1');
    setValue(element, '#room-note', '  ');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/rooms`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      number: '201',
      floor: 2,
      roomTypeId: 't-1',
      housekeepingStatus: 'Clean',
      isOutOfOrder: false,
      note: null,
    });
    expect(typeof request.request.body.floor).toBe('number');

    request.flush(CREATED_ROOM);
    await tick();

    // Basarili kayittan sonra listeye donulur.
    expect(navigate).toHaveBeenCalledWith(['/rooms']);
  });

  it('negatif etaji (bodrum) korur ve OutOfOrder secimini isOutOfOrder ile eslestirir', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    setValue(element, '#room-number', 'B01');
    setValue(element, '#room-floor', '-1');
    setValue(element, '#room-type', 't-1');
    setValue(element, '#room-status', 'OutOfOrder');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/rooms`);
    expect(request.request.body.floor).toBe(-1);
    expect(request.request.body.housekeepingStatus).toBe('OutOfOrder');
    expect(request.request.body.isOutOfOrder).toBe(true);
  });

  it('gecersiz etajda istek gondermez ve yonlendirme yapmaz', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');

    setValue(element, '#room-number', '201');
    // Sozlesme sinirlari: -5 … 99
    setValue(element, '#room-floor', '250');
    setValue(element, '#room-type', 't-1');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();
    fixture.detectChanges();

    http.expectNone((candidate) => candidate.url === `${baseUrl}/rooms`);
    expect(navigate).not.toHaveBeenCalled();
    expect(element.textContent).toContain('rooms.form.validation.floorRange');
  });
});

describe('RoomTypeFormPage — submit akisi', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  it('sayisal alanlari sayi olarak gonderir ve basaridan sonra listeye doner', async () => {
    const fixture = TestBed.createComponent(RoomTypeFormPage);
    fixture.detectChanges();
    await tick();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');

    // DE sekmesi varsayilan olarak aciktir (birincil dil).
    setValue(element, '#room-type-name-de', 'Doppelzimmer');
    setValue(element, '#room-type-code', 'DBL');
    setValue(element, '#room-type-base-price', '129.5');
    setValue(element, '#room-type-capacity', '2');
    setValue(element, '#room-type-size', '24');
    setValue(element, '#room-type-amenities', 'wifi, minibar, wifi');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/room-types`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      code: 'DBL',
      name: 'Doppelzimmer',
      description: null,
      basePrice: 129.5,
      capacity: 2,
      sizeSqm: 24,
      // Tekrar eden ozellik ayiklanir, sira korunur.
      amenities: ['wifi', 'minibar'],
      translations: { de: { name: 'Doppelzimmer', description: null } },
    });
    expect(typeof request.request.body.capacity).toBe('number');
    expect(typeof request.request.body.basePrice).toBe('number');

    request.flush({ ...ROOM_TYPE, basePrice: 129.5, sizeSqm: 24 });
    await tick();

    expect(navigate).toHaveBeenCalledWith(['/rooms/types']);
  });

  it('zorunlu DE adi eksikse istek gondermez', async () => {
    const fixture = TestBed.createComponent(RoomTypeFormPage);
    fixture.detectChanges();
    await tick();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    setValue(element, '#room-type-code', 'DBL');
    setValue(element, '#room-type-base-price', '129');
    setValue(element, '#room-type-capacity', '2');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();
    fixture.detectChanges();

    http.expectNone((candidate) => candidate.url === `${baseUrl}/room-types`);
    expect(element.textContent).toContain('rooms.types.validation.nameRequired');
  });
});
