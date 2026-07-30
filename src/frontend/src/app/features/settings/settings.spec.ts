import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type {
  HeadOfficeSettingsResponse,
  HotelListItemResponse,
  HotelResponse,
} from '../../core/models/settings.model';
import { SettingsPage } from './settings';

const HOTEL_ROW: HotelListItemResponse = {
  id: 'h-1',
  name: 'HotelCore Berlin Mitte',
  city: 'Berlin',
  country: 'DE',
  currency: 'EUR',
  defaultCulture: 'de',
  roomCount: 13,
};

const HOTEL: HotelResponse = {
  ...HOTEL_ROW,
  headOfficeId: 'ho-1',
  addressLine: 'Musterstraße 1',
  postalCode: '10117',
  phone: null,
  email: null,
  taxNumber: null,
  taxProfile: {
    vatRate: 19,
    reducedVatRate: 7,
    cityTaxPerPersonNight: 3.5,
    cityTaxEnabled: true,
  },
};

const HEAD_OFFICE: HeadOfficeSettingsResponse = {
  id: 'ho-1',
  brandName: 'HotelCore Demo Group',
  defaultCulture: 'de',
  hotelCount: 1,
};

/**
 * Uygulama **zoneless** oldugu icin `whenStable()` bekleyen promise'leri beklemez;
 * zincirli isteklerde (once `/hotels`, sonra `/hotels/{id}`) makrogorev sirasina
 * gecmek gerekir.
 */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('SettingsPage', () => {
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

  /** Ekrani canlandirir ve acilis isteklerini karsilar. */
  async function render(
    headOffice: HeadOfficeSettingsResponse | 'forbidden' = HEAD_OFFICE,
  ): Promise<ComponentFixture<SettingsPage>> {
    const fixture = TestBed.createComponent(SettingsPage);
    fixture.detectChanges();

    http.expectOne(`${baseUrl}/hotels`).flush([HOTEL_ROW]);

    const headOfficeRequest = http.expectOne(`${baseUrl}/head-office/settings`);
    if (headOffice === 'forbidden') {
      headOfficeRequest.flush(
        { status: 403, title: 'Forbidden' },
        { status: 403, statusText: 'Forbidden' },
      );
    } else {
      headOfficeRequest.flush(headOffice);
    }

    await tick();
    http.expectOne(`${baseUrl}/hotels/${HOTEL_ROW.id}`).flush(HOTEL);
    await tick();
    fixture.detectChanges();

    return fixture;
  }

  it('otel kunyesini ve vergi profilini forma yukler', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector<HTMLInputElement>('#settings-name')?.value).toBe(
      'HotelCore Berlin Mitte',
    );
    expect(element.querySelector<HTMLInputElement>('#settings-vat')?.value).toBe('19');
    expect(element.querySelector<HTMLInputElement>('#settings-city-tax')?.value).toBe('3.5');
    expect(element.querySelector<HTMLInputElement>('#settings-brand')?.value).toBe(
      'HotelCore Demo Group',
    );
  });

  it('tek otelde otel secicisini gostermez', async () => {
    const fixture = await render();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="settings-hotel-select"]'),
    ).toBeNull();
  });

  it('kaydederken para birimini buyuk harfe cevirir ve bos alanlari null gonderir', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    const currency = element.querySelector<HTMLInputElement>('#settings-currency');
    currency!.value = 'eur';
    currency!.dispatchEvent(new Event('input'));

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne(`${baseUrl}/hotels/${HOTEL_ROW.id}/settings`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.currency).toBe('EUR');
    // Formda bos duran alanlar "" yerine null olarak gider.
    expect(request.request.body.phone).toBeNull();
    expect(request.request.body.taxNumber).toBeNull();
    expect(request.request.body.taxProfile.cityTaxPerPersonNight).toBe(3.5);

    request.flush(HOTEL);
    await tick();
    // Kayittan sonra liste satiri (ad/sehir degismis olabilir) yenilenir.
    http.expectOne(`${baseUrl}/hotels`).flush([HOTEL_ROW]);
    await tick();
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="settings-hotel-saved"]')).not.toBeNull();
  });

  it('Head Office 403 verse bile otel ayarlari calismaya devam eder', async () => {
    const fixture = await render('forbidden');
    const element = fixture.nativeElement as HTMLElement;

    // Marka bolumu "kullanilamiyor" bilgisini gosterir...
    expect(element.querySelector('#settings-brand')).toBeNull();
    // ...ama otel formu yuklenmis durumdadir.
    expect(element.querySelector<HTMLInputElement>('#settings-name')?.value).toBe(
      'HotelCore Berlin Mitte',
    );
  });

  it('sunucudan gelen alan hatasini ilgili alana bagler', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    http.expectOne(`${baseUrl}/hotels/${HOTEL_ROW.id}/settings`).flush(
      {
        status: 400,
        title: 'Dogrulama hatasi.',
        errors: { Currency: ['Para birimi ISO 4217 bicimi olmalidir (3 harf).'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await tick();
    fixture.detectChanges();

    expect(element.textContent).toContain('ISO 4217');
  });
});
