import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, convertToParamMap, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import type { ShiftPlanResponse } from '../../core/models/shift.model';
import { AuthStore } from '../../core/state/auth.store';
import {
  currentIsoWeekLabel,
  isoWeekLabel,
  isoWeekOf,
  mondayOfIsoWeek,
  parseIsoWeekLabel,
  shiftIsoWeekLabel,
  toIsoDate,
  weeksInIsoYear,
} from './iso-week';
import { isCurrentIsoWeek, parseShiftWeekParam, shiftWeekToParams } from './shift-week-query';
import { ShiftsPage } from './shifts';

const WEEK = '2026-W32';

/** Sozlesmedeki gercek yanit sekli: gunler + otelin kadrosu. */
const PLAN: ShiftPlanResponse = {
  from: '2026-08-03',
  to: '2026-08-09',
  week: WEEK,
  days: [
    {
      date: '2026-08-03',
      shifts: [
        {
          id: 'sh-1',
          employeeId: 'emp-2',
          employeeName: 'Hedi Testfall',
          date: '2026-08-03',
          shiftType: 'Morning',
          note: 'Empfang',
        },
      ],
    },
    { date: '2026-08-04', shifts: [] },
    { date: '2026-08-05', shifts: [] },
    { date: '2026-08-06', shifts: [] },
    { date: '2026-08-07', shifts: [] },
    { date: '2026-08-08', shifts: [] },
    { date: '2026-08-09', shifts: [] },
  ],
  employees: [
    { id: 'emp-1', fullName: 'Anna Becker', departmentName: 'Wellness' },
    { id: 'emp-2', fullName: 'Hedi Testfall', departmentName: 'Housekeeping' },
  ],
};

const EMPTY_PLAN: ShiftPlanResponse = {
  ...PLAN,
  days: PLAN.days.map((day) => ({ ...day, shifts: [] })),
};

function user(permissions: readonly PermissionKey[]): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'klaus.meier@hotel.de',
    roles: ['Manager'],
    permissions,
    hotels: [{ id: 'h-1', name: 'Hotel Adler', currency: 'EUR' }],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
  };
}

/** Zoneless: `whenStable()` bekleyen promise'leri beklemez. */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('ShiftsPage — haftalik izgara', () => {
  let http: HttpTestingController;
  let baseUrl: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'shifts', component: ShiftsPage }]),
        provideTranslateService({ lang: 'de', fallbackLang: 'de' }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
  });

  /** Verilen haftada ekrani acar ve plan istegini karsilar. */
  async function render(
    permissions: readonly PermissionKey[],
    plan: ShiftPlanResponse = PLAN,
    week = WEEK,
  ): Promise<{ harness: RouterTestingHarness; element: HTMLElement }> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const harness = await RouterTestingHarness.create(`/shifts?week=${week}`);
    flushPlan(plan);
    await tick();
    harness.detectChanges();

    return { harness, element: harness.routeNativeElement as HTMLElement };
  }

  function flushPlan(plan: ShiftPlanResponse): void {
    http.expectOne((request) => request.url === `${baseUrl}/shifts`).flush(plan);
  }

  it('izgarayi gun basligi + calisan satiri olarak kurar (sutunlar birebir esit)', async () => {
    const { element } = await render([PERMISSIONS.ShiftsView]);

    const table = element.querySelector<HTMLTableElement>('table');
    // Baslik ve satirlar ayni tabloda durur; genislikler `table-fixed` ile esitlenir.
    expect(table?.classList.contains('table-fixed')).toBe(true);
    expect(element.querySelectorAll('[data-testid="shift-day-head"]')).toHaveLength(7);

    const headerCells = table!.querySelectorAll('thead th').length;
    const firstRowCells = table!.querySelectorAll('tbody tr:first-child > *').length;
    // Etiket sutunu + 7 gun; satirlar basliga birebir hizali olmali.
    expect(headerCells).toBe(8);
    expect(firstRowCells).toBe(headerCells);

    // Sticky etiket sutunu ve sticky baslik satiri.
    expect(table!.querySelector('thead th')?.className).toContain('sticky');
    expect(table!.querySelector('tbody th')?.className).toContain('sticky');
  });

  it('Shifts.Edit olmadan hucreleri salt okunur cizer', async () => {
    const { element } = await render([PERMISSIONS.ShiftsView]);

    expect(element.querySelectorAll('[data-testid="shift-cell"]')).toHaveLength(0);
    expect(element.querySelectorAll('[data-testid="shift-cell-readonly"]')).toHaveLength(14);
    expect(element.querySelector('[data-testid="shift-assign"]')).toBeNull();
  });

  it('hafta gezinmesini URL sorgu parametresine yazar', async () => {
    const { element, harness } = await render([PERMISSIONS.ShiftsView]);
    const router = TestBed.inject(Router);

    element.querySelector<HTMLButtonElement>('[data-testid="shift-week-next"] button')!.click();
    await tick();

    expect(router.url).toBe('/shifts?week=2026-W33');
    // Yeni hafta icin plan yeniden istenir.
    flushPlan({ ...PLAN, week: '2026-W33', from: '2026-08-10', to: '2026-08-16' });
    await tick();
    harness.detectChanges();
    expect(element.querySelector('[data-testid="shift-week-label"]')?.textContent?.trim()).toBe(
      '2026-W33',
    );

    element.querySelector<HTMLButtonElement>('[data-testid="shift-week-prev"] button')!.click();
    await tick();
    expect(router.url).toBe('/shifts?week=2026-W32');
    flushPlan(PLAN);
    await tick();
  });

  it('"bu hafta" dugmesi varsayilana doner (adres cubugunda hafta tasimaz)', async () => {
    const { element } = await render([PERMISSIONS.ShiftsView]);
    const router = TestBed.inject(Router);

    element.querySelector<HTMLButtonElement>('[data-testid="shift-week-current"] button')!.click();
    await tick();

    expect(router.url).toBe('/shifts');
    flushPlan({ ...PLAN, week: currentIsoWeekLabel() });
    await tick();
  });

  it('hucreye tiklayinca panel acilir ve vardiyayi POST eder', async () => {
    const { element, harness } = await render([PERMISSIONS.ShiftsView, PERMISSIONS.ShiftsEdit]);

    const cell = element.querySelector<HTMLButtonElement>(
      '[data-testid="shift-cell"][data-employee="emp-1"][data-date="2026-08-04"]',
    );
    expect(cell).not.toBeNull();
    cell!.click();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="shift-editor"]')).not.toBeNull();
    // Mevcut vardiya olmadigi icin silme dugmesi yok.
    expect(element.querySelector('[data-testid="shift-delete"]')).toBeNull();

    const type = element.querySelector<HTMLSelectElement>('#shift-type');
    type!.value = 'Night';
    type!.dispatchEvent(new Event('change'));

    element
      .querySelector<HTMLFormElement>('[data-testid="shift-editor"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/shifts`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      employeeId: 'emp-1',
      date: '2026-08-04',
      shiftType: 'Night',
      note: null,
    });
    request.flush(
      {
        id: 'sh-2',
        employeeId: 'emp-1',
        employeeName: 'Anna Becker',
        date: '2026-08-04',
        shiftType: 'Night',
        note: null,
      },
      { status: 201, statusText: 'Created' },
    );
    await tick();
    flushPlan(PLAN);
    await tick();
  });

  it('dolu hucrede PUT kullanir ve silme dugmesini gosterir', async () => {
    const { element, harness } = await render([PERMISSIONS.ShiftsView, PERMISSIONS.ShiftsEdit]);

    element
      .querySelector<HTMLButtonElement>(
        '[data-testid="shift-cell"][data-employee="emp-2"][data-date="2026-08-03"]',
      )!
      .click();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="shift-delete"]')).not.toBeNull();

    element
      .querySelector<HTMLFormElement>('[data-testid="shift-editor"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/shifts/sh-1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.shiftType).toBe('Morning');
    expect(request.request.body.note).toBe('Empfang');
    request.flush({ ...PLAN.days[0].shifts[0] });
    await tick();
    flushPlan(PLAN);
    await tick();
  });

  it('ayni gune ikinci vardiyada 409 yanitini anlamli mesaja cevirir', async () => {
    const { element, harness } = await render([PERMISSIONS.ShiftsView, PERMISSIONS.ShiftsEdit]);

    element
      .querySelector<HTMLButtonElement>(
        '[data-testid="shift-cell"][data-employee="emp-1"][data-date="2026-08-05"]',
      )!
      .click();
    harness.detectChanges();

    element
      .querySelector<HTMLFormElement>('[data-testid="shift-editor"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/shifts`)
      .flush(
        {
          status: 409,
          title: 'Islem mevcut durumla celisiyor.',
          detail: 'Bu calisanin 2026-08-05 gunu icin zaten bir vardiyasi var.',
        },
        { status: 409, statusText: 'Conflict' },
      );
    await tick();
    harness.detectChanges();

    expect(element.querySelector('[data-testid="shift-write-error"]')?.textContent).toContain(
      'shifts.editor.conflict',
    );
    expect(element.textContent).toContain('2026-08-05 gunu icin zaten bir vardiyasi var');
    // Panel acik kalir: kullanici baska bir gun/calisan secebilir.
    expect(element.querySelector('[data-testid="shift-editor"]')).not.toBeNull();
  });

  it('bos haftada izgarayi korur ve bos hafta bilgisini gosterir', async () => {
    const { element } = await render([PERMISSIONS.ShiftsView], EMPTY_PLAN);

    expect(element.querySelector('[data-testid="shift-week-empty"]')?.textContent).toContain(
      'shifts.emptyWeek',
    );
    // Izgara yine cizilir (planlama icin hucreler gerekir).
    expect(element.querySelectorAll('[data-testid="shift-day-head"]')).toHaveLength(7);
  });

  it('yukleme hatasinda yeniden dene sunar', async () => {
    TestBed.inject(AuthStore).setSession(user([PERMISSIONS.ShiftsView]));
    const harness = await RouterTestingHarness.create(`/shifts?week=${WEEK}`);
    http
      .expectOne((request) => request.url === `${baseUrl}/shifts`)
      .flush(
        { status: 500, title: 'Server error' },
        { status: 500, statusText: 'Internal Server Error' },
      );
    await tick();
    harness.detectChanges();

    const element = harness.routeNativeElement as HTMLElement;
    expect(element.textContent).toContain('shifts.loadFailed');
  });
});

describe('iso-week — ISO 8601 hafta hesabi', () => {
  it('gunun hafta etiketini kultureden bagimsiz uretir', () => {
    expect(isoWeekLabel(isoWeekOf(new Date(Date.UTC(2026, 7, 3))))).toBe('2026-W32');
    expect(isoWeekLabel(isoWeekOf(new Date(Date.UTC(2026, 7, 9))))).toBe('2026-W32');
    // Pazar biter, Pazartesi yeni hafta baslar.
    expect(isoWeekLabel(isoWeekOf(new Date(Date.UTC(2026, 7, 10))))).toBe('2026-W33');
  });

  it('yil sinirinda ISO kuralini uygular', () => {
    // 2026-01-01 Persembe -> 2026-W01; 2025-12-29 Pazartesi de ayni haftada.
    expect(isoWeekLabel(isoWeekOf(new Date(Date.UTC(2026, 0, 1))))).toBe('2026-W01');
    expect(isoWeekLabel(isoWeekOf(new Date(Date.UTC(2025, 11, 29))))).toBe('2026-W01');
    expect(toIsoDate(mondayOfIsoWeek(2026, 1))).toBe('2025-12-29');
    expect(toIsoDate(mondayOfIsoWeek(2026, 32))).toBe('2026-08-03');
  });

  it('53 haftali yillari tanir', () => {
    expect(weeksInIsoYear(2026)).toBe(53);
    expect(weeksInIsoYear(2025)).toBe(52);
    expect(parseIsoWeekLabel('2025-W53')).toBeNull();
    expect(parseIsoWeekLabel('2026-W53')).toEqual({ year: 2026, week: 53 });
  });

  it('hafta kaydirmasi yil sinirini asar', () => {
    expect(shiftIsoWeekLabel('2026-W01', -1)).toBe('2025-W52');
    expect(shiftIsoWeekLabel('2026-W53', 1)).toBe('2027-W01');
    expect(shiftIsoWeekLabel('2026-W32', 2)).toBe('2026-W34');
  });

  it('bicimsiz etiketi reddeder', () => {
    expect(parseIsoWeekLabel('2026-32')).toBeNull();
    expect(parseIsoWeekLabel('2026-W00')).toBeNull();
    expect(parseIsoWeekLabel(null)).toBeNull();
  });
});

describe('shift-week-query — URL senkronu', () => {
  const now = new Date(Date.UTC(2026, 7, 5));

  it('gecerli hafta parametresini okur', () => {
    expect(parseShiftWeekParam(convertToParamMap({ week: '2026-W40' }), now)).toBe('2026-W40');
  });

  it('gecersiz veya eksik parametrede bu haftaya duser', () => {
    expect(parseShiftWeekParam(convertToParamMap({ week: '2026-W99' }), now)).toBe('2026-W32');
    expect(parseShiftWeekParam(convertToParamMap({}), now)).toBe('2026-W32');
  });

  it('bulundugumuz haftayi adres cubuguna yazmaz', () => {
    expect(shiftWeekToParams('2026-W32', now)).toEqual({ week: null });
    expect(shiftWeekToParams('2026-W33', now)).toEqual({ week: '2026-W33' });
    expect(isCurrentIsoWeek('2026-W32', now)).toBe(true);
  });
});
