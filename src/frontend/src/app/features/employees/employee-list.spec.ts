import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import { EmployeesApi } from '../../core/api/employees.api';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import type { DepartmentResponse, EmployeeResponse } from '../../core/models/employee.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';
import {
  DEFAULT_EMPLOYEE_LIST_QUERY,
  employeeListQueryToParams,
  hasActiveEmployeeFilters,
  parseEmployeeListQuery,
} from './employee-list-query';
import { EmployeeListPage } from './employee-list';

const DEPARTMENT: DepartmentResponse = {
  id: 'dep-1',
  name: 'Rezeption',
  description: 'Empfang',
  employeeCount: 4,
};

const EMPLOYEE: EmployeeResponse = {
  id: 'emp-1',
  firstName: 'Anna',
  lastName: 'Becker',
  fullName: 'Anna Becker',
  email: 'anna@hotel.de',
  phone: null,
  staffNumber: 'P-014',
  departmentId: 'dep-1',
  departmentName: 'Rezeption',
  employmentType: 'FullTime',
  annualLeaveDays: 28,
  hiredOn: '2024-03-01',
  terminatedOn: null,
  isActive: true,
  userId: null,
};

/** Isten ayrilmis kayit: satir geri plana cekilir ama listede kalir. */
const FORMER_EMPLOYEE: EmployeeResponse = {
  ...EMPLOYEE,
  id: 'emp-2',
  firstName: 'Ben',
  lastName: 'Albers',
  fullName: 'Ben Albers',
  staffNumber: 'P-002',
  employmentType: 'Seasonal',
  terminatedOn: '2025-09-30',
  isActive: false,
};

const EMPLOYEE_PAGE: PagedResult<EmployeeResponse> = {
  items: [FORMER_EMPLOYEE, EMPLOYEE],
  page: 1,
  pageSize: 20,
  totalCount: 2,
};

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

describe('EmployeeListPage — RBAC gorunurlugu', () => {
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

  async function render(
    permissions: readonly PermissionKey[],
  ): Promise<ComponentFixture<EmployeeListPage>> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const fixture = TestBed.createComponent(EmployeeListPage);
    fixture.detectChanges();

    http.expectOne((request) => request.url === `${baseUrl}/departments`).flush([DEPARTMENT]);
    http.expectOne((request) => request.url === `${baseUrl}/employees`).flush(EMPLOYEE_PAGE);

    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('Employees.Edit izni olmadan yazma aksiyonlarini hic render etmez', async () => {
    const fixture = await render([PERMISSIONS.EmployeesView]);
    const element = fixture.nativeElement as HTMLElement;

    // Liste yine de gorunur (okuma izni var).
    expect(element.textContent).toContain('Anna Becker');
    expect(element.querySelectorAll('[data-testid="employee-edit"]')).toHaveLength(0);
    expect(element.querySelectorAll('[data-testid="employee-delete"]')).toHaveLength(0);
    expect(element.querySelector('[data-testid="employees-create"]')).toBeNull();
    expect(element.querySelector('[data-testid="employees-manage-departments"]')).toBeNull();
  });

  it('Employees.Edit izniyle olusturma, duzenleme ve silme aksiyonlarini gosterir', async () => {
    const fixture = await render([PERMISSIONS.EmployeesView, PERMISSIONS.EmployeesEdit]);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="employees-create"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="employees-manage-departments"]')).not.toBeNull();
    // Masaustu tablo + mobil kart ayni store'u okur; her ikisinde de aksiyon vardir.
    expect(element.querySelectorAll('[data-testid="employee-edit"]').length).toBeGreaterThan(0);
    expect(element.querySelectorAll('[data-testid="employee-delete"]').length).toBeGreaterThan(0);
    expect(
      element
        .querySelector<HTMLAnchorElement>('[data-testid="employee-edit"]')
        ?.getAttribute('href'),
    ).toBe('/employees/emp-2/edit');
  });

  it('isten ayrilmis satiri gorsel olarak geri plana ceker', async () => {
    const fixture = await render([PERMISSIONS.EmployeesView]);
    const element = fixture.nativeElement as HTMLElement;

    const inactiveRow = element.querySelector<HTMLElement>('tr[data-inactive="true"]');
    expect(inactiveRow).not.toBeNull();
    expect(inactiveRow?.classList.contains('opacity-60')).toBe(true);
    // Aktif kayitta isaret yoktur.
    expect(element.querySelectorAll('tr[data-inactive="true"]')).toHaveLength(1);
  });
});

describe('employee-list-query — URL <-> sorgu cozumlemesi', () => {
  it('gecerli parametreleri okur', () => {
    const query = parseEmployeeListQuery(
      convertToParamMap({
        page: '3',
        pageSize: '50',
        departmentId: 'dep-1',
        employmentType: 'MiniJob',
        search: '  becker ',
        includeTerminated: 'true',
      }),
    );

    expect(query).toEqual({
      page: 3,
      pageSize: 50,
      departmentId: 'dep-1',
      employmentType: 'MiniJob',
      search: 'becker',
      includeTerminated: true,
    });
    expect(hasActiveEmployeeFilters(query)).toBe(true);
  });

  it('gecersiz degerleri sessizce varsayilana duser', () => {
    const query = parseEmployeeListQuery(
      convertToParamMap({
        page: '0',
        pageSize: '7',
        employmentType: 'Freelancer',
        search: '   ',
        includeTerminated: 'yes',
      }),
    );

    expect(query).toEqual(DEFAULT_EMPLOYEE_LIST_QUERY);
    expect(hasActiveEmployeeFilters(query)).toBe(false);
  });

  it('varsayilan degerleri adres cubuguna yazmaz', () => {
    expect(employeeListQueryToParams(DEFAULT_EMPLOYEE_LIST_QUERY)).toEqual({});
    expect(
      employeeListQueryToParams({
        page: 2,
        pageSize: 100,
        departmentId: null,
        employmentType: 'PartTime',
        search: ' anna ',
        includeTerminated: true,
      }),
    ).toEqual({
      page: 2,
      pageSize: 100,
      employmentType: 'PartTime',
      search: 'anna',
      includeTerminated: true,
    });
  });
});

describe('EmployeesApi — sorgu dizesi', () => {
  let http: HttpTestingController;
  let baseUrl: string;
  let api: EmployeesApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    baseUrl = TestBed.inject(API_BASE_URL);
    api = TestBed.inject(EmployeesApi);
  });

  it('yalnizca dolu filtreleri gonderir', () => {
    api.list(DEFAULT_EMPLOYEE_LIST_QUERY).subscribe();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/employees`);
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    // Sunucu varsayilani `false` oldugu icin bayrak bos gonderilmez.
    expect(request.request.params.has('includeTerminated')).toBe(false);
    request.flush(EMPLOYEE_PAGE);
  });

  it('dolu filtreleri sozlesmedeki adlarla gonderir', () => {
    api
      .list({
        page: 2,
        pageSize: 50,
        departmentId: 'dep-1',
        employmentType: 'Apprentice',
        search: '  becker  ',
        includeTerminated: true,
      })
      .subscribe();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/employees`);
    const params = request.request.params;
    expect(params.get('page')).toBe('2');
    expect(params.get('pageSize')).toBe('50');
    expect(params.get('departmentId')).toBe('dep-1');
    expect(params.get('employmentType')).toBe('Apprentice');
    // Bosluklar kirpilir; sunucuya temiz arama metni gider.
    expect(params.get('search')).toBe('becker');
    expect(params.get('includeTerminated')).toBe('true');
    request.flush(EMPLOYEE_PAGE);
  });
});
