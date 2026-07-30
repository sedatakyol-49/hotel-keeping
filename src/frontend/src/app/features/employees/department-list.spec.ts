import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { AuthenticatedUser } from '../../core/models/auth.model';
import type { DepartmentResponse } from '../../core/models/employee.model';
import { PERMISSIONS, type PermissionKey } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';
import { DepartmentListPage } from './department-list';

const WELLNESS: DepartmentResponse = {
  id: 'dep-1',
  name: 'Wellness',
  description: 'Spa und Sauna',
  employeeCount: 1,
};

const KITCHEN: DepartmentResponse = {
  id: 'dep-2',
  name: 'Kitchen',
  description: null,
  employeeCount: 0,
};

function user(permissions: readonly PermissionKey[]): AuthenticatedUser {
  return {
    id: 'u-1',
    email: 'admin@hotelcore.local',
    roles: ['Admin'],
    permissions,
    hotels: [{ id: 'h-1', name: 'Hotel Adler', currency: 'EUR' }],
    canAccessAllHotels: false,
    defaultHotelId: 'h-1',
  };
}

/** Zoneless: zincirli isteklerde makrogorev sirasina gecmek gerekir. */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('DepartmentListPage', () => {
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
  ): Promise<ComponentFixture<DepartmentListPage>> {
    TestBed.inject(AuthStore).setSession(user(permissions));

    const fixture = TestBed.createComponent(DepartmentListPage);
    fixture.detectChanges();

    http
      .expectOne((request) => request.url === `${baseUrl}/departments`)
      .flush([WELLNESS, KITCHEN]);
    await tick();
    fixture.detectChanges();
    return fixture;
  }

  it('Employees.Edit izni olmadan yazma aksiyonlarini hic render etmez', async () => {
    const fixture = await render([PERMISSIONS.EmployeesView]);
    const element = fixture.nativeElement as HTMLElement;

    // Okuma izniyle liste ve calisan sayilari gorunur.
    expect(element.textContent).toContain('Wellness');
    expect(element.querySelector('[data-testid="department-create"]')).toBeNull();
    expect(element.querySelectorAll('[data-testid="department-edit"]')).toHaveLength(0);
    expect(element.querySelectorAll('[data-testid="department-delete"]')).toHaveLength(0);
  });

  it('silmede 409 yanitini "bagli calisanlar var" mesajina cevirir', async () => {
    const fixture = await render([PERMISSIONS.EmployeesView, PERMISSIONS.EmployeesEdit]);
    const element = fixture.nativeElement as HTMLElement;

    // Siralama ada gore: Kitchen, Wellness -> ikinci satir Wellness (1 calisan).
    const deleteButtons = element.querySelectorAll<HTMLElement>(
      'tbody [data-testid="department-delete"] button',
    );
    // Once satir ici onay istenir (dogrudan silme yoktur).
    deleteButtons[1].click();
    fixture.detectChanges();
    expect(element.textContent).toContain('employees.departments.delete.blocked');

    element
      .querySelector<HTMLElement>('tbody [data-testid="department-delete-confirm"] button')!
      .click();
    await tick();

    http.expectOne(`${baseUrl}/departments/${WELLNESS.id}`).flush(
      {
        status: 409,
        title: 'Islem mevcut durumla celisiyor.',
        detail: 'Bu departmana bagli calisanlar var; once onlari baska departmana tasiyin.',
      },
      { status: 409, statusText: 'Conflict' },
    );
    await tick();
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="department-delete-error"]')?.textContent).toContain(
      'employees.departments.delete.conflict',
    );
    // Kayit listede kalir (hard delete gerceklesmedi).
    expect(element.textContent).toContain('Wellness');
  });

  it('bagli calisani olmayan departmani listeden kaldirir', async () => {
    const fixture = await render([PERMISSIONS.EmployeesView, PERMISSIONS.EmployeesEdit]);
    const element = fixture.nativeElement as HTMLElement;

    const deleteButtons = element.querySelectorAll<HTMLElement>(
      'tbody [data-testid="department-delete"] button',
    );
    // Siralama ada gore: Kitchen, Wellness -> ikinci satir Wellness'tir.
    deleteButtons[0].click();
    fixture.detectChanges();
    expect(element.textContent).toContain('employees.departments.delete.confirmShort');

    element
      .querySelector<HTMLElement>('tbody [data-testid="department-delete-confirm"] button')!
      .click();
    await tick();

    http
      .expectOne(`${baseUrl}/departments/${KITCHEN.id}`)
      .flush(null, { status: 204, statusText: 'No Content' });
    await tick();
    fixture.detectChanges();

    expect(element.textContent).not.toContain('Kitchen');
    expect(element.textContent).toContain('Wellness');
  });
});
