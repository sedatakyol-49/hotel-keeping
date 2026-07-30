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
import type {
  VacationBalanceResponse,
  VacationRequestResponse,
} from '../../core/models/vacation.model';
import { AuthStore } from '../../core/state/auth.store';
import {
  DEFAULT_VACATION_LIST_QUERY,
  hasActiveVacationFilters,
  parseVacationListQuery,
  vacationListQueryToParams,
} from './vacation-list-query';
import { VacationListPage } from './vacation-list';

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

const PENDING: VacationRequestResponse = {
  id: 'vac-1',
  employeeId: 'emp-1',
  employeeName: 'Anna Becker',
  from: '2026-08-10',
  to: '2026-08-14',
  requestedDays: 5,
  status: 'Pending',
  reason: 'Sommerurlaub',
  decidedByUserId: null,
  decidedAt: null,
  decisionNote: null,
  createdAt: '2026-07-30T11:27:57+00:00',
};

/** Karara baglanmis talep: karar aksiyonlari gizlenmelidir (sunucu 409 dondururdu). */
const REJECTED: VacationRequestResponse = {
  ...PENDING,
  id: 'vac-2',
  status: 'Rejected',
  decidedByUserId: 'u-9',
  decidedAt: '2026-07-30T12:00:00+00:00',
  decisionNote: 'Besetzung',
};

const VACATION_PAGE: PagedResult<VacationRequestResponse> = {
  items: [PENDING, REJECTED],
  page: 1,
  pageSize: 20,
  totalCount: 2,
};

/** `id: null` — henuz kalici bakiye satiri yok; sayilar yine gecerlidir. */
const BALANCES: readonly VacationBalanceResponse[] = [
  {
    id: null,
    employeeId: 'emp-1',
    employeeName: 'Anna Becker',
    year: 2026,
    entitledDays: 28,
    usedDays: 5,
    carriedOverDays: 2,
    remainingDays: 25,
  },
];

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

describe('VacationListPage', () => {
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

  /** Ekrani canlandirir: kadro, liste ve bakiye isteklerini karsilar. */
  async function render(
    permissions: readonly PermissionKey[],
    page: PagedResult<VacationRequestResponse> = VACATION_PAGE,
  ): Promise<ComponentFixture<VacationListPage>> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const fixture = TestBed.createComponent(VacationListPage);
    fixture.detectChanges();

    http.expectOne((request) => request.url === `${baseUrl}/employees`).flush(EMPLOYEE_PAGE);
    http.expectOne((request) => request.url === `${baseUrl}/vacations`).flush(page);
    http.expectOne((request) => request.url === `${baseUrl}/vacations/balances`).flush(BALANCES);

    await tick();
    fixture.detectChanges();
    return fixture;
  }

  function click(element: HTMLElement, testId: string): void {
    element.querySelector<HTMLButtonElement>(`[data-testid="${testId}"] button`)!.click();
  }

  it('yalnizca Vacations.View ile hicbir yazma/karar aksiyonu render etmez', async () => {
    const fixture = await render([PERMISSIONS.VacationsView]);
    const element = fixture.nativeElement as HTMLElement;

    // Liste ve bakiye gorunur (okuma izni var).
    expect(element.textContent).toContain('Anna Becker');
    expect(element.querySelector('[data-testid="vacations-create"]')).toBeNull();
    expect(element.querySelectorAll('[data-testid="vacation-approve"]')).toHaveLength(0);
    expect(element.querySelectorAll('[data-testid="vacation-reject"]')).toHaveLength(0);
    expect(element.querySelectorAll('[data-testid="vacation-cancel"]')).toHaveLength(0);
  });

  it('Vacations.Approve ile karar aksiyonlarini yalnizca Pending satirda gosterir', async () => {
    const fixture = await render([PERMISSIONS.VacationsView, PERMISSIONS.VacationsApprove]);
    const element = fixture.nativeElement as HTMLElement;

    // Iki yerlesim (masaustu tablo + mobil kart) ayni store'u okur -> satir basina 2 dugme.
    expect(element.querySelectorAll('[data-testid="vacation-approve"]')).toHaveLength(2);
    expect(element.querySelectorAll('[data-testid="vacation-reject"]')).toHaveLength(2);
    // Onaylayan her talebi iptal edebilir (sunucu: Approve -> tum talepler).
    expect(element.querySelectorAll('[data-testid="vacation-cancel"]')).toHaveLength(2);
    // Talep olusturma `Vacations.Request` ister.
    expect(element.querySelector('[data-testid="vacations-create"]')).toBeNull();
  });

  it('Vacations.Request ile iptali ve talep olusturmayi gosterir, karari gostermez', async () => {
    const fixture = await render([PERMISSIONS.VacationsView, PERMISSIONS.VacationsRequest]);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="vacations-create"]')).not.toBeNull();
    expect(element.querySelectorAll('[data-testid="vacation-cancel"]')).toHaveLength(2);
    expect(element.querySelectorAll('[data-testid="vacation-approve"]')).toHaveLength(0);
  });

  it('onaydan sonra listeyi **ve** bakiyeyi yeniden yukler', async () => {
    const fixture = await render([PERMISSIONS.VacationsView, PERMISSIONS.VacationsApprove]);
    const element = fixture.nativeElement as HTMLElement;

    click(element, 'vacation-approve');

    const approve = http.expectOne(
      (request) => request.url === `${baseUrl}/vacations/vac-1/approve`,
    );
    expect(approve.request.method).toBe('POST');
    // Not verilmediyse govde bostur (sozlesme: `decisionNote` opsiyonel).
    expect(approve.request.body).toEqual({});
    approve.flush({ ...PENDING, status: 'Approved' });
    await tick();

    // Bakiye onayla degistigi icin ikisi birlikte tazelenir.
    http
      .expectOne((request) => request.url === `${baseUrl}/vacations`)
      .flush({ ...VACATION_PAGE, items: [{ ...PENDING, status: 'Approved' }, REJECTED] });
    http
      .expectOne((request) => request.url === `${baseUrl}/vacations/balances`)
      .flush([{ ...BALANCES[0], usedDays: 10, remainingDays: 20 }]);
    await tick();
    fixture.detectChanges();

    expect(
      element.querySelector('[data-testid="vacation-balance-remaining"]')?.textContent?.trim(),
    ).toBe('20');
  });

  it('ret gerekcesini decisionNote olarak gonderir', async () => {
    const fixture = await render([PERMISSIONS.VacationsView, PERMISSIONS.VacationsApprove]);
    const element = fixture.nativeElement as HTMLElement;

    click(element, 'vacation-reject');
    fixture.detectChanges();

    const note = element.querySelector<HTMLTextAreaElement>(
      '[data-testid="vacation-decision-note"]',
    );
    note!.value = '  Besetzung an der Rezeption  ';
    note!.dispatchEvent(new Event('input'));

    click(element, 'vacation-reject-confirm');

    const reject = http.expectOne((request) => request.url === `${baseUrl}/vacations/vac-1/reject`);
    expect(reject.request.body).toEqual({ decisionNote: 'Besetzung an der Rezeption' });
    reject.flush({ ...PENDING, status: 'Rejected' });
    await tick();
  });

  it('409 yanitini "artik karar verilemez" mesajina cevirir', async () => {
    const fixture = await render([PERMISSIONS.VacationsView, PERMISSIONS.VacationsApprove]);
    const element = fixture.nativeElement as HTMLElement;

    click(element, 'vacation-approve');

    http
      .expectOne((request) => request.url === `${baseUrl}/vacations/vac-1/approve`)
      .flush(
        {
          status: 409,
          title: 'Islem mevcut durumla celisiyor.',
          detail: 'Bu izin talebi zaten karara baglandi (durum: Approved).',
        },
        { status: 409, statusText: 'Conflict' },
      );
    await tick();
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="vacation-action-error"]')?.textContent).toContain(
      'vacations.decide.conflict',
    );
    // Sunucunun aciklamasi da gosterilir (hangi durumda oldugu yazili).
    expect(element.textContent).toContain('zaten karara baglandi');
  });

  it('bakiye satiri kalici degilse (id: null) bunu isaretler ama sayilari gosterir', async () => {
    const fixture = await render([PERMISSIONS.VacationsView]);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="vacation-balance-derived"]')).not.toBeNull();
    expect(
      element.querySelector('[data-testid="vacation-balance-remaining"]')?.textContent?.trim(),
    ).toBe('25');
    expect(element.querySelector('[data-testid="vacations-calendar-note"]')?.textContent).toContain(
      'vacations.calendarDaysNote',
    );
  });

  it('bos liste durumunda karar aksiyonu icermeyen bos durum blogu gosterir', async () => {
    const fixture = await render([PERMISSIONS.VacationsView, PERMISSIONS.VacationsApprove], {
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
    });
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('vacations.empty.title');
    expect(element.querySelectorAll('[data-testid="vacation-approve"]')).toHaveLength(0);
  });
});

describe('vacation-list-query — URL <-> sorgu cozumlemesi', () => {
  it('gecerli parametreleri okur', () => {
    const query = parseVacationListQuery(
      convertToParamMap({
        page: '2',
        pageSize: '50',
        employeeId: 'emp-1',
        status: 'Approved',
        year: '2026',
        from: '2026-08-01',
        to: '2026-08-31',
      }),
    );

    expect(query).toEqual({
      page: 2,
      pageSize: 50,
      employeeId: 'emp-1',
      status: 'Approved',
      year: 2026,
      from: '2026-08-01',
      to: '2026-08-31',
    });
    expect(hasActiveVacationFilters(query)).toBe(true);
  });

  it('gecersiz degerleri sessizce varsayilana duser', () => {
    const query = parseVacationListQuery(
      convertToParamMap({
        page: '0',
        pageSize: '7',
        status: 'Archived',
        year: '1900',
        from: '2026-13-01',
      }),
    );

    expect(query).toEqual(DEFAULT_VACATION_LIST_QUERY);
    expect(hasActiveVacationFilters(query)).toBe(false);
  });

  it('ters tarih araliginda `to` degerini dusurur (sunucu 400 dondururdu)', () => {
    const query = parseVacationListQuery(
      convertToParamMap({ from: '2026-09-10', to: '2026-09-01' }),
    );

    expect(query.from).toBe('2026-09-10');
    expect(query.to).toBeNull();
  });

  it('varsayilan degerleri adres cubuguna yazmaz', () => {
    expect(vacationListQueryToParams(DEFAULT_VACATION_LIST_QUERY)).toEqual({});
    expect(
      vacationListQueryToParams({
        page: 3,
        pageSize: 100,
        employeeId: 'emp-1',
        status: 'Pending',
        year: 2027,
        from: null,
        to: null,
      }),
    ).toEqual({
      page: 3,
      pageSize: 100,
      employeeId: 'emp-1',
      status: 'Pending',
      year: 2027,
    });
  });
});
