import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API_BASE_URL } from '../../core/api/api-base';
import type { EmployeeResponse } from '../../core/models/employee.model';
import type { PagedResult } from '../../core/models/paged-result.model';
import { VacationFormPage, calendarDaysBetween } from './vacation-form';

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

/** Zoneless: `whenStable()` bekleyen promise'leri beklemez. */
function tick(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('VacationFormPage', () => {
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

  async function render(): Promise<ComponentFixture<VacationFormPage>> {
    const fixture = TestBed.createComponent(VacationFormPage);
    fixture.detectChanges();

    http.expectOne((request) => request.url === `${baseUrl}/employees`).flush(EMPLOYEE_PAGE);
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

  function fillValidForm(element: HTMLElement): void {
    setValue(element, '#vacation-employee', 'emp-1');
    setValue(element, '#vacation-from', '2026-08-10');
    setValue(element, '#vacation-to', '2026-08-14');
  }

  function submit(element: HTMLElement): void {
    element.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
  }

  it('to < from ise istek gondermez ve alan hatasi gosterir', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    setValue(element, '#vacation-employee', 'emp-1');
    setValue(element, '#vacation-from', '2026-09-10');
    setValue(element, '#vacation-to', '2026-09-01');

    submit(element);
    await tick();
    fixture.detectChanges();

    // Sunucuya hic gidilmez: capraz alan kurali istemcide yakalanir.
    http.expectNone((request) => request.url === `${baseUrl}/vacations`);
    expect(element.querySelector('[data-testid="vacation-period-error"]')?.textContent).toContain(
      'vacations.form.validation.periodOrder',
    );
    expect(element.querySelector('#vacation-to')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('tek gunluk talebi (to = from) kabul eder ve bos gerekceyi null gonderir', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');

    setValue(element, '#vacation-employee', 'emp-1');
    setValue(element, '#vacation-from', '2026-08-10');
    setValue(element, '#vacation-to', '2026-08-10');
    setValue(element, '#vacation-reason', '   ');

    submit(element);
    await tick();

    const request = http.expectOne((candidate) => candidate.url === `${baseUrl}/vacations`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      employeeId: 'emp-1',
      from: '2026-08-10',
      to: '2026-08-10',
      reason: null,
    });

    request.flush({}, { status: 201, statusText: 'Created' });
    await tick();
    expect(navigate).toHaveBeenCalledWith(['/vacations']);
  });

  it('409 cakismasini "bu tarihlerde baska talep var" mesajina cevirir', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    fillValidForm(element);
    submit(element);
    await tick();

    http
      .expectOne((candidate) => candidate.url === `${baseUrl}/vacations`)
      .flush(
        {
          status: 409,
          title: 'Islem mevcut durumla celisiyor.',
          detail: 'Bu calisanin 2026-08-10 - 2026-08-14 araliginda bekleyen bir izni var.',
        },
        { status: 409, statusText: 'Conflict' },
      );
    await tick();
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="vacation-form-error"]')?.textContent).toContain(
      'vacations.form.overlap',
    );
    // Cakisma tarih alanina da baglanir ve sunucunun aciklamasi gosterilir.
    expect(element.textContent).toContain('vacations.form.validation.overlap');
    expect(element.querySelector('[data-testid="vacation-form-detail"]')?.textContent).toContain(
      '2026-08-10 - 2026-08-14',
    );
  });

  it('takvim gunu onizlemesini gosterir (hafta sonu dusulmez)', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    fillValidForm(element);
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="vacation-days-preview"]')?.textContent).toContain(
      'vacations.form.daysPreview',
    );
  });

  it('calisan secilmediyse istek gondermez', async () => {
    const fixture = await render();
    const element = fixture.nativeElement as HTMLElement;

    setValue(element, '#vacation-from', '2026-08-10');
    setValue(element, '#vacation-to', '2026-08-14');

    submit(element);
    await tick();
    fixture.detectChanges();

    http.expectNone((request) => request.url === `${baseUrl}/vacations`);
    expect(element.querySelector('[data-testid="vacation-employee-error"]')?.textContent).toContain(
      'vacations.form.validation.employeeIdRequired',
    );
  });
});

describe('calendarDaysBetween — takvim gunu', () => {
  it('her iki ucu sayar', () => {
    expect(calendarDaysBetween('2026-08-10', '2026-08-14')).toBe(5);
    expect(calendarDaysBetween('2026-08-10', '2026-08-10')).toBe(1);
  });

  it('yaz saati gecisinde de gun sayisini kaydirmaz (UTC hesabi)', () => {
    // 2026-03-29 Avrupa'da yaz saatine gecis gunudur.
    expect(calendarDaysBetween('2026-03-28', '2026-03-30')).toBe(3);
  });

  it('ters aralikta ve gecersiz tarihte null doner', () => {
    expect(calendarDaysBetween('2026-09-10', '2026-09-01')).toBeNull();
    expect(calendarDaysBetween('2026-02-30', '2026-03-01')).toBeNull();
    expect(calendarDaysBetween('', '')).toBeNull();
  });
});
