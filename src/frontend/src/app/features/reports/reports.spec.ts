import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, convertToParamMap, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import { addDays } from '../../core/models/availability.model';
import type {
  OccupancyReportResponse,
  RevenueReportResponse,
} from '../../core/models/report.model';
import { ReportsPage } from './reports';
import {
  clampReportRange,
  isQuickRangeActive,
  parseReportRange,
  quickReportRange,
  rangeDayCount,
  reportRangeToParams,
} from './reports-query';

const FROM = '2026-09-01';
const TO = '2026-09-07';

const SCOPE = {
  mode: 'Hotel',
  hotelId: 'h-1',
  hotelCount: 1,
  currency: 'EUR',
  hasMixedCurrencies: false,
} as const;

const OCCUPANCY: OccupancyReportResponse = {
  from: FROM,
  to: TO,
  dayCount: 7,
  scope: SCOPE,
  roomCount: 12,
  outOfOrderRoomCount: 2,
  physicalRoomNights: 84,
  outOfOrderRoomNights: 14,
  availableRoomNights: 70,
  soldRoomNights: 14,
  occupancyRate: 20,
  daily: [
    { date: '2026-09-01', soldRoomNights: 2, availableRoomNights: 10, occupancyRate: 20 },
    { date: '2026-09-02', soldRoomNights: 2, availableRoomNights: 10, occupancyRate: 20 },
    { date: '2026-09-03', soldRoomNights: 2, availableRoomNights: 10, occupancyRate: 20 },
    { date: '2026-09-04', soldRoomNights: 2, availableRoomNights: 10, occupancyRate: 20 },
    { date: '2026-09-05', soldRoomNights: 2, availableRoomNights: 10, occupancyRate: 20 },
    { date: '2026-09-06', soldRoomNights: 2, availableRoomNights: 10, occupancyRate: 20 },
    { date: '2026-09-07', soldRoomNights: 2, availableRoomNights: 10, occupancyRate: 20 },
  ],
  byHotel: [
    {
      hotelId: 'h-1',
      hotelName: 'HotelCore Berlin Mitte',
      roomCount: 12,
      outOfOrderRoomCount: 2,
      physicalRoomNights: 84,
      outOfOrderRoomNights: 14,
      availableRoomNights: 70,
      soldRoomNights: 14,
      occupancyRate: 20,
    },
  ],
};

const REVENUE: RevenueReportResponse = {
  from: FROM,
  to: TO,
  dayCount: 7,
  scope: SCOPE,
  soldRoomNights: 14,
  availableRoomNights: 70,
  outOfOrderRoomNights: 14,
  physicalRoomNights: 84,
  occupancyRate: 20,
  roomRevenue: { net: 2112.14, vat: 147.86, gross: 2260 },
  extraRevenue: { net: 0, vat: 0, gross: 0 },
  totalRevenue: { net: 2112.14, vat: 147.86, gross: 2260 },
  cityTaxCollected: 39,
  adrNet: 150.87,
  adrGross: 161.43,
  revParNet: 30.17,
  revParGross: 32.29,
  unbilledRoomRevenueGross: 258,
  otherInvoicedRevenue: {
    room: { net: 0, vat: 0, gross: 0 },
    extra: { net: 84.03, vat: 15.97, gross: 100 },
    total: { net: 84.03, vat: 15.97, gross: 100 },
    cityTaxCollected: 0,
  },
  byChannel: [
    {
      channel: 'Direct',
      reservationCount: 3,
      soldRoomNights: 7,
      roomRevenue: { net: 2112.14, vat: 147.86, gross: 2260 },
      extraRevenue: { net: 0, vat: 0, gross: 0 },
      cityTaxCollected: 39,
      adrNet: 301.73,
      roomRevenueShare: 100,
    },
    {
      // Kesinlesmis fatura + Stornorechnung birlikte sayilir: tam sifir eder.
      channel: 'BookingCom',
      reservationCount: 1,
      soldRoomNights: 7,
      roomRevenue: { net: 0, vat: 0, gross: 0 },
      extraRevenue: { net: 0, vat: 0, gross: 0 },
      cityTaxCollected: 0,
      adrNet: 0,
      roomRevenueShare: 0,
    },
  ],
  byHotel: [
    {
      hotelId: 'h-1',
      hotelName: 'HotelCore Berlin Mitte',
      currency: 'EUR',
      soldRoomNights: 14,
      availableRoomNights: 70,
      occupancyRate: 20,
      roomRevenue: { net: 2112.14, vat: 147.86, gross: 2260 },
      extraRevenue: { net: 0, vat: 0, gross: 0 },
      totalRevenue: { net: 2112.14, vat: 147.86, gross: 2260 },
      cityTaxCollected: 39,
      adrNet: 150.87,
      revParNet: 30.17,
    },
  ],
  daily: [
    {
      date: '2026-09-01',
      soldRoomNights: 2,
      availableRoomNights: 10,
      occupancyRate: 20,
      roomRevenue: { net: 407.48, vat: 28.53, gross: 436.01 },
      extraRevenue: { net: 0, vat: 0, gross: 0 },
      cityTaxCollected: 9,
      adrNet: 203.74,
      revParNet: 40.75,
    },
  ],
};

/** Zoneless: `whenStable()` bekleyen promise'leri beklemez. */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

type Flush<T> = T | 'error';

describe('ReportsPage — dogruluk raporlari', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'reports', component: ReportsPage }]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  function flushBoth(
    occupancy: Flush<OccupancyReportResponse> = OCCUPANCY,
    revenue: Flush<RevenueReportResponse> = REVENUE,
  ): void {
    const occupancyRequest = http.expectOne(
      (request) => request.url === `${baseUrl}/reports/occupancy`,
    );
    const revenueRequest = http.expectOne(
      (request) => request.url === `${baseUrl}/reports/revenue`,
    );

    if (occupancy === 'error') {
      occupancyRequest.flush(
        { title: 'Server error', status: 500 },
        { status: 500, statusText: 'Server Error' },
      );
    } else {
      occupancyRequest.flush(occupancy);
    }

    if (revenue === 'error') {
      revenueRequest.flush(
        { title: 'Server error', status: 500 },
        { status: 500, statusText: 'Server Error' },
      );
    } else {
      revenueRequest.flush(revenue);
    }
  }

  async function render(
    url = `/reports?from=${FROM}&to=${TO}`,
    occupancy: Flush<OccupancyReportResponse> = OCCUPANCY,
    revenue: Flush<RevenueReportResponse> = REVENUE,
  ): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    const harness = await RouterTestingHarness.create(url);
    flushBoth(occupancy, revenue);
    await tick();
    harness.detectChanges();

    return { harness, element: harness.routeNativeElement as HTMLElement };
  }

  it('366 gunu asan donemi ISTEMCIDE kirpar ve sunucudan gecersiz aralik istemez', async () => {
    // Kullanici elle iki yillik bir adres yazsa bile sunucuya 400 aldiracak
    // istek hic gonderilmez.
    const harness = await RouterTestingHarness.create('/reports?from=2026-01-01&to=2027-12-31');

    const occupancyRequest = http.expectOne(
      (request) => request.url === `${baseUrl}/reports/occupancy`,
    );
    const revenueRequest = http.expectOne(
      (request) => request.url === `${baseUrl}/reports/revenue`,
    );

    // 366 gunluk kapali aralik: 2026-01-01 … 2027-01-01 (from + 365).
    expect(occupancyRequest.request.params.get('from')).toBe('2026-01-01');
    expect(occupancyRequest.request.params.get('to')).toBe('2027-01-01');
    expect(revenueRequest.request.params.get('to')).toBe('2027-01-01');

    occupancyRequest.flush({ ...OCCUPANCY, from: '2026-01-01', to: '2027-01-01', dayCount: 366 });
    revenueRequest.flush({ ...REVENUE, from: '2026-01-01', to: '2027-01-01', dayCount: 366 });
    await tick();
    harness.detectChanges();

    // Kirpma sessizce yapilmaz; kullaniciya aciklanir.
    const element = harness.routeNativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="reports-clamped"]')?.textContent).toContain(
      'reports.range.clamped',
    );
  });

  it('secilen donemi adres cubuguna yazar (?from=&to=)', async () => {
    const { harness, element } = await render();

    const fromInput = element.querySelector<HTMLInputElement>('[data-testid="reports-range-from"]');
    expect(fromInput?.value).toBe(FROM);

    fromInput!.value = '2026-08-15';
    fromInput!.dispatchEvent(new Event('change'));
    await tick();
    await tick();
    harness.detectChanges();

    // Rapor baglantisi paylasilabilir olmalidir: donem her zaman URL'e yazilir.
    expect(TestBed.inject(Router).url).toBe('/reports?from=2026-08-15&to=2026-09-07');

    // Yeni donem iki ucta da yeniden sorulur.
    flushBoth();
    await tick();
    harness.detectChanges();
  });

  it('Kurtaxe`yi ciro toplamina KATMAZ, ayri ve aciklamali gosterir', async () => {
    const { element } = await render();

    const total = element.querySelector('[data-testid="reports-total-revenue"]');
    const kpiTotal = element.querySelector('[data-testid="reports-kpi-total-revenue"]');
    const cityTax = element.querySelector('[data-testid="reports-city-tax"]');

    // Toplam ciro = 2.112,14 (oda + ekstra). Kurtaxe eklenseydi 2.151,14 olurdu.
    expect(total?.textContent).toContain('2.112,14');
    expect(total?.textContent).not.toContain('2.151,14');
    expect(kpiTotal?.textContent).toContain('2.112,14');
    expect(kpiTotal?.textContent).not.toContain('2.151,14');

    // Kurtaxe ayri bir blokta ve "gelir degildir" aciklamasiyla durur.
    expect(cityTax?.textContent).toContain('39,00');
    expect(element.textContent).toContain('reports.cityTax.hint');
    expect(element.textContent).toContain('reports.excluded.title');
  });

  it('faturalanmamis konaklamalari hicbir toplama katmaz, ayri gosterir', async () => {
    const { element } = await render();

    const unbilled = element.querySelector('[data-testid="reports-unbilled"]');
    expect(unbilled?.textContent).toContain('258,00');
    expect(element.textContent).toContain('reports.unbilled.hint');

    // Ne ciro toplaminda ne de sozlesmenin izin verdigi muhasebe toplaminda.
    const total = element.querySelector('[data-testid="reports-total-revenue"]');
    expect(total?.textContent).not.toContain('2.370,14'); // 2112,14 + 258,00

    // Muhasebe toplami = totalRevenue.net + otherInvoicedRevenue.total.net
    // = 2.112,14 + 84,03 = 2.196,17 (Kurtaxe ve faturalanmamis tutar YOK).
    const accounting = element.querySelector('[data-testid="reports-accounting-total"]');
    expect(accounting?.textContent).toContain('2.196,17');
    expect(element.querySelector('[data-testid="reports-other-revenue"]')?.textContent).toContain(
      '84,03',
    );
  });

  it('doluluk %100`u astiginda sayiyi kirpmaz, aciklamayi gosterir', async () => {
    const overCapacity: OccupancyReportResponse = {
      ...OCCUPANCY,
      soldRoomNights: 84,
      availableRoomNights: 70,
      occupancyRate: 120,
      daily: [
        { date: '2026-09-01', soldRoomNights: 12, availableRoomNights: 10, occupancyRate: 120 },
      ],
    };
    const { element } = await render(`/reports?from=${FROM}&to=${TO}`, overCapacity);

    expect(element.querySelector('[data-testid="reports-over-capacity"]')?.textContent).toContain(
      'reports.occupancy.overCapacity',
    );
    // Sayi gizlenmez/kirpilmaz.
    expect(element.querySelector('[data-testid="reports-kpi-occupancy"]')?.textContent).toContain(
      '120,00',
    );
    // Cubuk 100'de durur ama satirdaki oran oldugu gibi kalir.
    const row = element.querySelector('[data-testid="reports-daily-row"]');
    expect(row?.textContent).toContain('120,00');
    expect(row?.querySelector<HTMLElement>('span[style]')?.style.width).toBe('100%');
  });

  it('bir uc hata verdiginde digerini render etmeye devam eder', async () => {
    const { element } = await render(`/reports?from=${FROM}&to=${TO}`, OCCUPANCY, 'error');

    // Ciro bolumu hata blogu gosterir …
    expect(element.querySelector('[data-testid="reports-revenue-error"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="reports-revenue-table"]')).toBeNull();

    // … doluluk bolumu calismaya devam eder.
    expect(element.querySelector('[data-testid="reports-occupancy-error"]')).toBeNull();
    expect(element.querySelectorAll('[data-testid="reports-daily-row"]')).toHaveLength(7);
    expect(element.querySelector('[data-testid="reports-kpi-occupancy"]')?.textContent).toContain(
      '20,00',
    );
    // Para KPI'lari kaynagi olmadigi icin "—" gosterir, uydurma sayi uretilmez.
    expect(element.querySelector('[data-testid="reports-kpi-adr-net"]')?.textContent).toContain(
      '—',
    );
  });

  it('ters yonde de calisir: doluluk duserse ciro bolumu ayakta kalir', async () => {
    const { element } = await render(`/reports?from=${FROM}&to=${TO}`, 'error', REVENUE);

    expect(element.querySelector('[data-testid="reports-occupancy-error"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="reports-daily-table"]')).toBeNull();
    expect(element.querySelector('[data-testid="reports-revenue-error"]')).toBeNull();
    expect(element.querySelector('[data-testid="reports-adr-gross"]')?.textContent).toContain(
      '161,43',
    );
  });

  it('gunluk seriyi tablo olarak sunar; oransal dolgu ekran okuyucudan gizlidir', async () => {
    const { element } = await render();

    const table = element.querySelector('[data-testid="reports-daily-table"]');
    expect(table?.tagName).toBe('TABLE');
    expect(table?.querySelector('caption')?.textContent).toContain('reports.daily.caption');

    const rows = element.querySelectorAll('[data-testid="reports-daily-row"]');
    expect(rows).toHaveLength(OCCUPANCY.daily.length);
    // Her satir kendi tarihini ve sayilarini tasir (yalnizca gorsele bagli degil).
    expect(rows[0].getAttribute('data-date')).toBe('2026-09-01');
    expect(rows[0].querySelector('th')?.getAttribute('scope')).toBe('row');
    expect(rows[0].textContent).toContain('20,00');

    const bar = rows[0].querySelector('span[aria-hidden="true"]');
    expect(bar).not.toBeNull();
    expect(bar?.querySelector<HTMLElement>('span')?.style.width).toBe('20%');

    // Yuvarlama dipnotu: gunluk toplamlar ust toplamdan sapabilir.
    expect(element.querySelector('[data-testid="reports-rounding-note"]')?.textContent).toContain(
      'reports.daily.roundingNote',
    );
  });

  it('kapasite ucluusunu ve dolulugun paydasini birlikte gosterir', async () => {
    const { element } = await render();

    const capacity = element.querySelector('[data-testid="reports-capacity"]');
    expect(
      capacity?.querySelector('[data-testid="reports-capacity-physical"]')?.textContent,
    ).toContain('84');
    expect(
      capacity?.querySelector('[data-testid="reports-capacity-out-of-order"]')?.textContent,
    ).toContain('14');
    expect(
      capacity?.querySelector('[data-testid="reports-capacity-available"]')?.textContent,
    ).toContain('70');
    expect(element.textContent).toContain('reports.capacity.availableHint');
  });

  it('0,00 gelirli (stornolu) kanali gizlemez', async () => {
    const { element } = await render();

    const rows = element.querySelectorAll('[data-testid="reports-channel-row"]');
    expect(rows).toHaveLength(2);
    expect([...rows].map((row) => row.getAttribute('data-channel'))).toEqual([
      'Direct',
      'BookingCom',
    ]);
    expect(rows[1].textContent).toContain('0,00');
    expect(element.querySelector('[data-testid="reports-channel-note"]')?.textContent).toContain(
      'reports.channels.zeroNote',
    );
  });

  it('konsolide + karisik para biriminde tutarlari sembolsuz gosterir ve uyarir', async () => {
    const mixedScope = {
      mode: 'Consolidated',
      hotelId: null,
      hotelCount: 3,
      currency: null,
      hasMixedCurrencies: true,
    } as const;
    const { element } = await render(
      `/reports?from=${FROM}&to=${TO}`,
      { ...OCCUPANCY, scope: mixedScope },
      { ...REVENUE, scope: mixedScope },
    );

    expect(element.querySelector('[data-testid="reports-scope-mode"]')?.textContent).toContain(
      'reports.scope.mode.consolidated',
    );
    expect(element.querySelector('[data-testid="reports-mixed-currency"]')?.textContent).toContain(
      'reports.scope.mixedCurrencies',
    );

    // Ust toplam farkli birimlerin toplamidir: sayi gizlenmez ama yanlis bir
    // sembolle etiketlenmez.
    const kpiTotal = element.querySelector('[data-testid="reports-kpi-total-revenue"]');
    expect(kpiTotal?.textContent).toContain('2.112,14');
    expect(kpiTotal?.textContent).not.toContain('€');

    // Otel kirilimi kendi para birimiyle gosterilir (esas alinacak sayi odur).
    expect(
      element.querySelector('[data-testid="reports-revenue-hotel-row"]')?.textContent,
    ).toContain('€');
  });

  it('bos donemde gecerli bir rapor gosterir (404 yok, uydurma sayi yok)', async () => {
    const emptyOccupancy: OccupancyReportResponse = {
      ...OCCUPANCY,
      roomCount: 0,
      outOfOrderRoomCount: 0,
      physicalRoomNights: 0,
      outOfOrderRoomNights: 0,
      availableRoomNights: 0,
      soldRoomNights: 0,
      occupancyRate: 0,
      daily: [],
      byHotel: [],
    };
    const emptyRevenue: RevenueReportResponse = {
      ...REVENUE,
      roomRevenue: { net: 0, vat: 0, gross: 0 },
      extraRevenue: { net: 0, vat: 0, gross: 0 },
      totalRevenue: { net: 0, vat: 0, gross: 0 },
      cityTaxCollected: 0,
      adrNet: 0,
      adrGross: 0,
      revParNet: 0,
      revParGross: 0,
      unbilledRoomRevenueGross: 0,
      otherInvoicedRevenue: {
        room: { net: 0, vat: 0, gross: 0 },
        extra: { net: 0, vat: 0, gross: 0 },
        total: { net: 0, vat: 0, gross: 0 },
        cityTaxCollected: 0,
      },
      byChannel: [],
      byHotel: [],
      daily: [],
    };

    const { element } = await render(
      `/reports?from=${FROM}&to=${TO}`,
      emptyOccupancy,
      emptyRevenue,
    );

    expect(element.textContent).toContain('reports.occupancy.empty.title');
    expect(element.textContent).toContain('reports.revenue.empty.title');
    expect(element.querySelector('[data-testid="reports-daily-table"]')).toBeNull();
    // Hata degil: KPI seridi sifirlarla gecerli kalir.
    expect(element.querySelector('[data-testid="reports-occupancy-error"]')).toBeNull();
    expect(element.querySelector('[data-testid="reports-kpi-occupancy"]')?.textContent).toContain(
      '0,00',
    );
  });
});

describe('reports-query — 366 gun tavani ve donem senkronu', () => {
  const now = new Date(Date.UTC(2026, 8, 15)); // 2026-09-15

  it('kapali araligi dogru sayar (to DAHIL)', () => {
    expect(rangeDayCount({ from: '2026-09-01', to: '2026-09-07' })).toBe(7);
    // Tek gunluk rapor gecerlidir.
    expect(rangeDayCount({ from: '2026-09-01', to: '2026-09-01' })).toBe(1);
  });

  it('sunucu tavanini asan donemi kirpar ve bayrak birakir', () => {
    const clamped = clampReportRange('2026-01-01', '2027-12-31');
    expect(clamped.to).toBe('2027-01-01');
    expect(rangeDayCount(clamped)).toBe(366);
    expect(clamped.clamped).toBe(true);

    const within = clampReportRange('2026-01-01', '2026-12-31');
    expect(within.to).toBe('2026-12-31');
    expect(within.clamped).toBe(false);
  });

  it('ters araligi tek gunluk rapora dusurur', () => {
    expect(clampReportRange('2026-09-10', '2026-09-01')).toMatchObject({
      from: '2026-09-10',
      to: '2026-09-10',
    });
  });

  it('gecersiz/eksik parametrede son 30 gune duser', () => {
    const range = parseReportRange(convertToParamMap({ from: 'kein-datum' }), now);
    expect(range.from).toBe('2026-08-17');
    expect(range.to).toBe('2026-09-15');
    expect(rangeDayCount(range)).toBe(30);
  });

  it('hazir donemleri sozlesmeye uygun uretir', () => {
    expect(quickReportRange('last7', now)).toMatchObject({ from: '2026-09-09', to: '2026-09-15' });
    expect(quickReportRange('last30', now)).toMatchObject({ from: '2026-08-17', to: '2026-09-15' });
    // "Bu ay" bilincli olarak bugunde biter: gerceklesmemis gunler RevPAR
    // paydasini sismezsin diye.
    expect(quickReportRange('thisMonth', now)).toMatchObject({
      from: '2026-09-01',
      to: '2026-09-15',
    });
    expect(quickReportRange('lastMonth', now)).toMatchObject({
      from: '2026-08-01',
      to: '2026-08-31',
    });

    expect(isQuickRangeActive({ from: '2026-09-01', to: '2026-09-15' }, 'thisMonth', now)).toBe(
      true,
    );
    expect(isQuickRangeActive({ from: '2026-09-01', to: '2026-09-14' }, 'thisMonth', now)).toBe(
      false,
    );
  });

  it('donemi her zaman URL`e yazar (paylasilan baglanti ayni donemi gostersin)', () => {
    expect(reportRangeToParams({ from: '2026-09-01', to: '2026-09-07' })).toEqual({
      from: '2026-09-01',
      to: '2026-09-07',
    });
    // Varsayilan donem de yazilir: "son 30 gun" gorece bir penceredir ve yarin
    // baska bir donemi gosterirdi.
    const fallback = parseReportRange(convertToParamMap({}), now);
    expect(reportRangeToParams(fallback)).toEqual({
      from: addDays('2026-09-15', -29),
      to: '2026-09-15',
    });
  });
});
