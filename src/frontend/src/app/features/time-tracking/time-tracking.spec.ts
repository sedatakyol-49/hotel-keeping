import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import type { EmployeeResponse } from '../../core/models/employee.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import {
  formatWorkedMinutes,
  fromDateTimeLocalValue,
  grossMinutesBetween,
  toDateTimeLocalValue,
  type TimeEntryResponse,
} from '../../core/models/time-entry.model';
import { AuthStore } from '../../core/state/auth.store';
import {
  DEFAULT_TIME_ENTRY_LIST_QUERY,
  parseTimeEntryListQuery,
  timeEntryListQueryToParams,
} from './time-entry-list-query';
import { TimeTrackingPage } from './time-tracking';

const EMPLOYEE: EmployeeResponse = {
  id: 'emp-1',
  firstName: 'Anna',
  lastName: 'Becker',
  fullName: 'Anna Becker',
  departmentId: 'dep-1',
  departmentName: 'Rezeption',
  employmentType: 'FullTime',
  annualLeaveDays: 28,
  hiredOn: '2024-03-01',
  isActive: true,
};

const EMPLOYEE_PAGE: PagedResult<EmployeeResponse> = {
  items: [EMPLOYEE],
  page: 1,
  pageSize: 200,
  totalCount: 1,
};

/** Kapali kayit: `workedMinutes` 480 -> "8:00". */
const CLOSED_ENTRY: TimeEntryResponse = {
  id: 'te-1',
  employeeId: 'emp-1',
  employeeName: 'Anna Becker',
  clockIn: '2026-07-29T06:00:00+00:00',
  clockOut: '2026-07-29T14:30:00+00:00',
  breakMinutes: 30,
  workedMinutes: 480,
  source: 'Manual',
  note: null,
  isOpen: false,
};

/** Acik kayit: sure yerine "devam ediyor" gostergesi cizilir. */
const OPEN_ENTRY: TimeEntryResponse = {
  id: 'te-2',
  employeeId: 'emp-1',
  employeeName: 'Anna Becker',
  clockIn: '2026-07-30T06:00:00+00:00',
  clockOut: null,
  breakMinutes: 0,
  workedMinutes: null,
  source: 'Manual',
  note: null,
  isOpen: true,
};

function page(items: readonly TimeEntryResponse[]): PagedResult<TimeEntryResponse> {
  return { items, page: 1, pageSize: 20, totalCount: items.length };
}

function user(permissions: readonly PermissionKey[]): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'klaus.meier@hotel.de',
    roles: ['Receptionist'],
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

describe('TimeTrackingPage', () => {
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

  /** Liste istegi (`pageSize` 20) — acik kayit yoklamasi `pageSize=1` kullanir. */
  function expectListRequest(items: readonly TimeEntryResponse[]): void {
    http
      .expectOne(
        (request) =>
          request.url === `${baseUrl}/time-entries` && request.params.get('pageSize') !== '1',
      )
      .flush(page(items));
  }

  async function render(
    permissions: readonly PermissionKey[],
    items: readonly TimeEntryResponse[] = [CLOSED_ENTRY],
  ): Promise<ComponentFixture<TimeTrackingPage>> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const fixture = TestBed.createComponent(TimeTrackingPage);
    fixture.detectChanges();

    http.expectOne((request) => request.url === `${baseUrl}/employees`).flush(EMPLOYEE_PAGE);
    expectListRequest(items);

    await tick();
    fixture.detectChanges();
    return fixture;
  }

  /** Giris/cikis panelinde calisan secer ve acik kayit yoklamasini karsilar. */
  async function selectClockEmployee(
    fixture: ComponentFixture<TimeTrackingPage>,
    latest: TimeEntryResponse | null,
  ): Promise<void> {
    const element = fixture.nativeElement as HTMLElement;
    const select = element.querySelector<HTMLSelectElement>('[data-testid="clock-employee"]');
    select!.value = 'emp-1';
    select!.dispatchEvent(new Event('change'));

    const probe = http.expectOne(
      (request) =>
        request.url === `${baseUrl}/time-entries` && request.params.get('pageSize') === '1',
    );
    expect(probe.request.params.get('employeeId')).toBe('emp-1');
    probe.flush(page(latest ? [latest] : []));

    await tick();
    fixture.detectChanges();
  }

  function click(element: HTMLElement, testId: string): void {
    element.querySelector<HTMLButtonElement>(`[data-testid="${testId}"] button`)!.click();
  }

  it('workedMinutes degerini saat:dakika olarak bicimlendirir', async () => {
    const fixture = await render([PERMISSIONS.TimeTrackingView]);
    const element = fixture.nativeElement as HTMLElement;

    const worked = element.querySelectorAll<HTMLElement>('[data-testid="time-worked"]');
    expect(worked.length).toBeGreaterThan(0);
    expect(worked[0].textContent?.trim()).toBe('8:00');
    expect(element.querySelectorAll('[data-testid="time-open"]')).toHaveLength(0);
  });

  it('acik kayitta sure yerine "devam ediyor" gostergesi cizer', async () => {
    const fixture = await render([PERMISSIONS.TimeTrackingView], [OPEN_ENTRY]);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelectorAll('[data-testid="time-worked"]')).toHaveLength(0);
    expect(element.querySelector('[data-testid="time-open"]')?.textContent).toContain(
      'timeTracking.open',
    );
    expect(element.querySelector('tr[data-open="true"]')).not.toBeNull();
  });

  it('acik kaydi olan calisanda clock-in dugmesini hic render etmez', async () => {
    const fixture = await render(
      [PERMISSIONS.TimeTrackingView, PERMISSIONS.TimeTrackingRecord],
      [OPEN_ENTRY],
    );
    const element = fixture.nativeElement as HTMLElement;

    await selectClockEmployee(fixture, OPEN_ENTRY);

    // Sunucudan 409 almak normal akis degil: dugme zaten gorunmez.
    expect(element.querySelector('[data-testid="clock-in"]')).toBeNull();
    expect(element.querySelector('[data-testid="clock-out"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="clock-open-since"]')?.textContent).toContain(
      'timeTracking.clock.openSince',
    );
  });

  it('acik kaydi olmayan calisanda yalnizca clock-in gosterir ve gonderir', async () => {
    const fixture = await render([PERMISSIONS.TimeTrackingView, PERMISSIONS.TimeTrackingRecord]);
    const element = fixture.nativeElement as HTMLElement;

    await selectClockEmployee(fixture, CLOSED_ENTRY);

    expect(element.querySelector('[data-testid="clock-out"]')).toBeNull();
    expect(element.querySelector('[data-testid="clock-in"]')).not.toBeNull();

    click(element, 'clock-in');

    const clockIn = http.expectOne((request) => request.url === `${baseUrl}/time-entries/clock-in`);
    expect(clockIn.request.method).toBe('POST');
    expect(clockIn.request.body).toEqual({ employeeId: 'emp-1', note: null });
    clockIn.flush(OPEN_ENTRY, { status: 201, statusText: 'Created' });
    await tick();

    // Basaridan sonra liste ve acik kayit tazelenir.
    expectListRequest([OPEN_ENTRY]);
    http
      .expectOne(
        (request) =>
          request.url === `${baseUrl}/time-entries` && request.params.get('pageSize') === '1',
      )
      .flush(page([OPEN_ENTRY]));
    await tick();
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="clock-in"]')).toBeNull();
    expect(element.querySelector('[data-testid="clock-out"]')).not.toBeNull();
  });

  it('TimeTracking.Record izni olmadan giris/cikis panelini ve satir aksiyonlarini gizler', async () => {
    const fixture = await render([PERMISSIONS.TimeTrackingView]);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="time-clock-panel"]')).toBeNull();
    expect(element.querySelectorAll('[data-testid="time-edit"]')).toHaveLength(0);
    expect(element.querySelectorAll('[data-testid="time-delete"]')).toHaveLength(0);
  });

  it('mola calisma suresini asarsa istek gondermez ve mevcut sureyi soyler', async () => {
    const fixture = await render([PERMISSIONS.TimeTrackingView, PERMISSIONS.TimeTrackingRecord]);
    const element = fixture.nativeElement as HTMLElement;

    click(element, 'time-edit');
    fixture.detectChanges();

    setValue(element, '#time-edit-clock-in', '2026-07-29T06:00');
    setValue(element, '#time-edit-clock-out', '2026-07-29T14:00');
    setValue(element, '#time-edit-break', '600');
    fixture.detectChanges();

    element
      .querySelector<HTMLFormElement>('[data-testid="time-edit-form"]')!
      .dispatchEvent(new Event('submit'));
    await tick();
    fixture.detectChanges();

    http.expectNone((request) => request.url === `${baseUrl}/time-entries/te-1`);
    const message = element.querySelector('[data-testid="time-break-error"]')?.textContent ?? '';
    expect(message).toContain('timeTracking.form.validation.breakExceedsWork');
  });

  it('gecerli duzeltmeyi PUT eder ve sunucunun alan mesajini alana bagler', async () => {
    const fixture = await render([PERMISSIONS.TimeTrackingView, PERMISSIONS.TimeTrackingRecord]);
    const element = fixture.nativeElement as HTMLElement;

    click(element, 'time-edit');
    fixture.detectChanges();

    setValue(element, '#time-edit-clock-in', '2026-07-29T06:00');
    setValue(element, '#time-edit-clock-out', '2026-07-29T14:00');
    setValue(element, '#time-edit-break', '30');
    setValue(element, '#time-edit-note', ' Korrektur ');

    element
      .querySelector<HTMLFormElement>('[data-testid="time-edit-form"]')!
      .dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/time-entries/te-1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.breakMinutes).toBe(30);
    expect(request.request.body.note).toBe('Korrektur');
    // Yerel saat ISO'ya cevrilir; aradaki brut sure korunur (8 saat).
    const gross =
      new Date(request.request.body.clockOut).getTime() -
      new Date(request.request.body.clockIn).getTime();
    expect(gross).toBe(8 * 60 * 60 * 1000);

    request.flush(
      {
        status: 400,
        title: 'Dogrulama hatasi.',
        errors: { BreakMinutes: ['Mola suresi calisma suresini (480 dk) asamaz.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await tick();
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="time-break-server-error"]')?.textContent).toContain(
      'Mola suresi calisma suresini (480 dk) asamaz.',
    );
  });

  it('silme islemini onay sonrasi gonderir', async () => {
    const fixture = await render([PERMISSIONS.TimeTrackingView, PERMISSIONS.TimeTrackingRecord]);
    const element = fixture.nativeElement as HTMLElement;

    click(element, 'time-delete');
    fixture.detectChanges();
    // Onay istenmeden istek gitmez.
    http.expectNone((request) => request.url === `${baseUrl}/time-entries/te-1`);

    click(element, 'time-delete-confirm');
    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/time-entries/te-1`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });
    await tick();

    expectListRequest([]);
    await tick();
    fixture.detectChanges();
    expect(element.textContent).toContain('timeTracking.empty.title');
  });

  function setValue(element: HTMLElement, selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(selector);
    input!.value = value;
    input!.dispatchEvent(new Event('input'));
    input!.dispatchEvent(new Event('change'));
  }
});

describe('formatWorkedMinutes', () => {
  it('dakikayi saat:dakika olarak yazar', () => {
    expect(formatWorkedMinutes(480)).toBe('8:00');
    expect(formatWorkedMinutes(65)).toBe('1:05');
    expect(formatWorkedMinutes(0)).toBe('0:00');
    expect(formatWorkedMinutes(1445)).toBe('24:05');
  });

  it('acik kayitta (null) deger uretmez', () => {
    expect(formatWorkedMinutes(null)).toBeNull();
    expect(formatWorkedMinutes(undefined)).toBeNull();
  });
});

describe('datetime-local <-> ISO donusumu', () => {
  it('gidis-donus donusumde ayni ani korur', () => {
    const local = toDateTimeLocalValue('2026-07-29T06:00:00+00:00');
    expect(local).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/);

    const iso = fromDateTimeLocalValue(local);
    expect(iso).not.toBeNull();
    expect(new Date(iso!).getTime()).toBe(new Date('2026-07-29T06:00:00+00:00').getTime());
  });

  it('bos ve gecersiz degerleri guvenle karsilar', () => {
    expect(toDateTimeLocalValue(null)).toBe('');
    expect(toDateTimeLocalValue('nicht-ein-datum')).toBe('');
    expect(fromDateTimeLocalValue('')).toBeNull();
    expect(fromDateTimeLocalValue('nicht-ein-datum')).toBeNull();
  });

  it('brut dakikayi hesaplar', () => {
    expect(grossMinutesBetween('2026-07-29T06:00:00Z', '2026-07-29T14:30:00Z')).toBe(510);
    expect(grossMinutesBetween('2026-07-29T06:00:00Z', null)).toBeNull();
  });
});

describe('time-entry-list-query — URL <-> sorgu cozumlemesi', () => {
  it('gecerli parametreleri okur, gecersizleri varsayilana duser', () => {
    expect(
      parseTimeEntryListQuery(
        convertToParamMap({ page: '2', pageSize: '50', employeeId: 'emp-1', from: '2026-07-01' }),
      ),
    ).toEqual({ page: 2, pageSize: 50, employeeId: 'emp-1', from: '2026-07-01', to: null });

    expect(
      parseTimeEntryListQuery(convertToParamMap({ page: '-1', pageSize: '13', from: 'gestern' })),
    ).toEqual(DEFAULT_TIME_ENTRY_LIST_QUERY);
  });

  it('varsayilan degerleri adres cubuguna yazmaz', () => {
    expect(timeEntryListQueryToParams(DEFAULT_TIME_ENTRY_LIST_QUERY)).toEqual({});
    expect(
      timeEntryListQueryToParams({
        page: 2,
        pageSize: 100,
        employeeId: 'emp-1',
        from: '2026-07-01',
        to: '2026-07-31',
      }),
    ).toEqual({
      page: 2,
      pageSize: 100,
      employeeId: 'emp-1',
      from: '2026-07-01',
      to: '2026-07-31',
    });
  });
});
