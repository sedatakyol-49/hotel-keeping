import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { DepartmentResponse } from '../../core/models/employee.model';
import { EmployeeFormPage } from './employee-form';

const DEPARTMENT: DepartmentResponse = {
  id: 'dep-1',
  name: 'Rezeption',
  description: null,
  employeeCount: 4,
};

/**
 * Uygulama **zoneless** oldugu icin `whenStable()` bekleyen promise'leri
 * beklemez; makrogorev sirasina gecmek gerekir.
 */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('EmployeeFormPage — istemci dogrulamasi', () => {
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

  /** Olusturma modunda ekrani canlandirir ve departman listesini karsilar. */
  async function render(): Promise<ComponentFixture<EmployeeFormPage>> {
    const fixture = TestBed.createComponent(EmployeeFormPage);
    fixture.detectChanges();

    http.expectOne((request) => request.url === `${baseUrl}/departments`).flush([DEPARTMENT]);
    await tick();
    fixture.detectChanges();
    return fixture;
  }

  function setValue(element: HTMLElement, selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
    input!.value = value;
    input!.dispatchEvent(new Event('input'));
    input!.dispatchEvent(new Event('change'));
  }

  /** Gecerli bir kayit doldurur; testler tek alani bozarak dogrulamayi olcer. */
  function fillValidForm(element: HTMLElement): void {
    setValue(element, '#employee-first-name', 'Anna');
    setValue(element, '#employee-last-name', 'Becker');
    setValue(element, '#employee-department', 'dep-1');
    setValue(element, '#employee-annual-leave', '28');
    setValue(element, '#employee-hired-on', '2024-03-01');
  }

  it('terminatedOn < hiredOn ise istek gondermez ve alan hatasi gosterir', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    fillValidForm(element);
    setValue(element, '#employee-terminated-on', '2023-12-31');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();
    fixture.detectChanges();

    // Sunucuya hic gidilmez: capraz alan kurali istemcide yakalanir.
    http.expectNone((request) => request.url === `${baseUrl}/employees`);
    expect(
      element.querySelector('[data-testid="employee-terminated-error"]')?.textContent,
    ).toContain('employees.form.validation.terminatedBeforeHired');
    expect(element.querySelector('#employee-terminated-on')?.getAttribute('aria-invalid')).toBe(
      'true',
    );
  });

  it('terminatedOn = hiredOn kabul edilir ve bos alanlar null olarak gonderilir', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    fillValidForm(element);
    setValue(element, '#employee-terminated-on', '2024-03-01');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/employees`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      firstName: 'Anna',
      lastName: 'Becker',
      email: null,
      phone: null,
      staffNumber: null,
      departmentId: 'dep-1',
      employmentType: 'FullTime',
      annualLeaveDays: 28,
      hiredOn: '2024-03-01',
      terminatedOn: '2024-03-01',
    });
  });

  it('yillik izin sinirini (0-60) istemcide uygular', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    fillValidForm(element);
    setValue(element, '#employee-annual-leave', '61');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();
    fixture.detectChanges();

    http.expectNone((request) => request.url === `${baseUrl}/employees`);
    expect(element.textContent).toContain('employees.form.validation.annualLeaveDaysRange');
  });

  it('409 yanitini personel numarasi alanina bagler', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    fillValidForm(element);
    setValue(element, '#employee-staff-number', 'P-014');

    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/employees`)
      .flush(
        {
          status: 409,
          title: 'Islem mevcut durumla celisiyor.',
          detail: "'P-014' personel numarasi bu otelde zaten kullaniliyor.",
        },
        { status: 409, statusText: 'Conflict' },
      );
    await tick();
    fixture.detectChanges();

    expect(element.textContent).toContain('employees.form.validation.staffNumberConflict');
    expect(element.querySelector('[data-testid="employee-form-error"]')?.textContent).toContain(
      'employees.form.conflict',
    );
  });

  it('sunucudan gelen PascalCase alan hatasini ilgili alana bagler', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    fillValidForm(element);
    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await tick();

    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/employees`)
      .flush(
        {
          status: 400,
          title: 'Dogrulama hatasi.',
          errors: { StaffNumber: ['Die Länge von Staff Number muss kleiner als 20 sein.'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
    await tick();
    fixture.detectChanges();

    expect(element.textContent).toContain('Die Länge von Staff Number');
  });
});
