import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { EmployeesApi } from '../../core/api/employees.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import {
  EMPLOYEE_LIMITS,
  EMPLOYMENT_TYPES,
  EMPLOYMENT_TYPE_LABEL_KEYS,
  type EmployeeWriteRequest,
  type EmploymentType,
} from '../../core/models/employee.model';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  applyApiFieldErrors,
  serverErrorMessages,
  setServerError,
} from '../../shared/forms/api-field-errors';
import { dateOrderValidator, isoDateValidator } from '../../shared/forms/date-validators';
import { decimalRangeValidator, parseDecimal } from '../../shared/forms/numeric-validators';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { DepartmentsStore } from './departments.store';

type EmployeeFormControl =
  | 'firstName'
  | 'lastName'
  | 'email'
  | 'phone'
  | 'staffNumber'
  | 'departmentId'
  | 'employmentType'
  | 'annualLeaveDays'
  | 'hiredOn'
  | 'terminatedOn';

/** `terminatedOn < hiredOn` — grup seviyesinde tutulan capraz alan hatasi. */
const DATE_ORDER_ERROR = 'terminatedBeforeHired';

/**
 * Calisan olustur/duzenle (`POST /employees`, `PUT /employees/{id}`).
 *
 * Sozlesmedeki dogrulama kurallari istemcide de birebir uygulanir; yine de son
 * soz backend'dedir: 400 yanitindaki `errors` sozlugu ilgili alanlara (PascalCase
 * -> camelCase cozumlemesiyle), 409 (`staffNumber` cakismasi) personel numarasi
 * alanina baglanir.
 */
@Component({
  selector: 'hc-employee-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, PageHeader, Button, Spinner],
  templateUrl: './employee-form.html',
})
export class EmployeeFormPage {
  private readonly api = inject(EmployeesApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly departments = inject(DepartmentsStore);

  /** Rota parametresi (`withComponentInputBinding`); olusturma modunda bostur. */
  readonly id = input<string | undefined>(undefined);

  protected readonly employmentTypes = EMPLOYMENT_TYPES;
  protected readonly employmentTypeLabelKeys = EMPLOYMENT_TYPE_LABEL_KEYS;
  protected readonly limits = EMPLOYEE_LIMITS;
  /** Dogrulama mesajlarinda kullanilan ortak interpolasyon parametreleri. */
  protected readonly validationParams = {
    nameMaxLength: EMPLOYEE_LIMITS.firstNameMaxLength,
    emailMaxLength: EMPLOYEE_LIMITS.emailMaxLength,
    phoneMaxLength: EMPLOYEE_LIMITS.phoneMaxLength,
    staffNumberMaxLength: EMPLOYEE_LIMITS.staffNumberMaxLength,
    min: EMPLOYEE_LIMITS.annualLeaveDaysMin,
    max: EMPLOYEE_LIMITS.annualLeaveDaysMax,
  };

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly formErrorKey = signal<string | null>(null);
  /** Alanla eslesmeyen sunucu mesajlari (backend tarafinda cevrilmis gelir). */
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly submitted = signal(false);

  protected readonly isEdit = computed(() => Boolean(this.id()));

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      firstName: [
        '',
        [Validators.required, Validators.maxLength(EMPLOYEE_LIMITS.firstNameMaxLength)],
      ],
      lastName: [
        '',
        [Validators.required, Validators.maxLength(EMPLOYEE_LIMITS.lastNameMaxLength)],
      ],
      email: ['', [Validators.email, Validators.maxLength(EMPLOYEE_LIMITS.emailMaxLength)]],
      phone: ['', [Validators.maxLength(EMPLOYEE_LIMITS.phoneMaxLength)]],
      staffNumber: ['', [Validators.maxLength(EMPLOYEE_LIMITS.staffNumberMaxLength)]],
      departmentId: ['', [Validators.required]],
      employmentType: ['FullTime' as EmploymentType, [Validators.required]],
      annualLeaveDays: [
        '',
        [
          Validators.required,
          decimalRangeValidator({
            min: EMPLOYEE_LIMITS.annualLeaveDaysMin,
            max: EMPLOYEE_LIMITS.annualLeaveDaysMax,
          }),
        ],
      ],
      hiredOn: ['', [Validators.required, isoDateValidator()]],
      terminatedOn: ['', [isoDateValidator()]],
    },
    // Sozlesme: `terminatedOn` >= `hiredOn`.
    { validators: [dateOrderValidator('hiredOn', 'terminatedOn', DATE_ORDER_ERROR)] },
  );

  /** Capraz alan hatasi grupta durur; mesaj bitis tarihi alaninda gosterilir. */
  protected readonly dateOrderInvalid = computed(() => {
    const touched = this.form.controls.terminatedOn.touched || this.submitted();
    return touched && this.form.errors?.[DATE_ORDER_ERROR] === true;
  });

  constructor() {
    void this.departments.load();

    effect(() => {
      const id = this.id();
      if (id) {
        void this.fetch(id);
      }
    });
  }

  protected reload(): void {
    const id = this.id();
    if (id) {
      void this.fetch(id);
    }
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.formErrorKey.set(null);
    this.formErrors.set([]);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: EmployeeWriteRequest = {
      firstName: raw.firstName.trim(),
      lastName: raw.lastName.trim(),
      // Bos metin yerine `null` gonderilir; backend alanlari nullable'dir.
      email: raw.email.trim() || null,
      phone: raw.phone.trim() || null,
      staffNumber: raw.staffNumber.trim() || null,
      departmentId: raw.departmentId,
      employmentType: raw.employmentType,
      annualLeaveDays: parseDecimal(raw.annualLeaveDays) ?? 0,
      hiredOn: raw.hiredOn,
      terminatedOn: raw.terminatedOn || null,
    };

    this.saving.set(true);
    try {
      const id = this.id();
      if (id) {
        await firstValueFrom(this.api.update(id, request));
      } else {
        await firstValueFrom(this.api.create(request));
      }
      // Listeye donuldugunde `EmployeeListPage` sorguyu yeniden calistirir.
      await this.router.navigate(['/employees']);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(['/employees']);
  }

  /** Validator hatasi -> i18n anahtari (`conflict` hatasi anahtarin kendisini tasir). */
  protected errorKeyFor(controlName: EmployeeFormControl): string | null {
    if (controlName === 'terminatedOn' && this.dateOrderInvalid()) {
      return 'employees.form.validation.terminatedBeforeHired';
    }

    const control = this.form.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (typeof errors['conflict'] === 'string') {
      return errors['conflict'];
    }
    if (errors['required']) {
      return `employees.form.validation.${controlName}Required`;
    }
    if (errors['email']) {
      return 'employees.form.validation.emailInvalid';
    }
    if (errors['maxlength']) {
      return `employees.form.validation.${controlName}Length`;
    }
    if (errors['decimalFormat']) {
      return 'employees.form.validation.annualLeaveDaysFormat';
    }
    if (errors['decimalRange']) {
      return 'employees.form.validation.annualLeaveDaysRange';
    }
    if (errors['dateFormat']) {
      return 'employees.form.validation.dateFormat';
    }
    return null;
  }

  protected serverMessagesFor(controlName: EmployeeFormControl): readonly string[] {
    return serverErrorMessages(this.form.get(controlName));
  }

  private async fetch(id: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const employee = await firstValueFrom(this.api.getById(id));
      this.form.reset({
        firstName: employee.firstName,
        lastName: employee.lastName,
        email: employee.email ?? '',
        phone: employee.phone ?? '',
        staffNumber: employee.staffNumber ?? '',
        departmentId: employee.departmentId,
        employmentType: employee.employmentType,
        annualLeaveDays: String(employee.annualLeaveDays),
        // Sozlesme tarih (saat yok) doner: `<input type="date">` ile birebir uyumlu.
        hiredOn: employee.hiredOn,
        terminatedOn: employee.terminatedOn ?? '',
      });
      this.submitted.set(false);
    } catch (error: unknown) {
      this.loadError.set(toApiError(error));
    } finally {
      this.loading.set(false);
    }
  }

  private handleWriteError(error: ApiError): void {
    if (error.status === 409) {
      // Sozlesmede tek 409 sebebi `staffNumber` cakismasidir.
      setServerError(
        this.form.controls.staffNumber,
        'employees.form.validation.staffNumberConflict',
      );
      this.formErrorKey.set('employees.form.conflict');
      return;
    }

    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
