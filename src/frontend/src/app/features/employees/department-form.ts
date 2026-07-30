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

import { DepartmentsApi } from '../../core/api/departments.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import { DEPARTMENT_LIMITS, type DepartmentWriteRequest } from '../../core/models/employee.model';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  applyApiFieldErrors,
  serverErrorMessages,
  setServerError,
} from '../../shared/forms/api-field-errors';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { DepartmentsStore } from './departments.store';

type DepartmentFormControl = 'name' | 'description';

/**
 * Departman olustur/duzenle (`POST /departments`, `PUT /departments/{id}`).
 *
 * Sozlesmede **tek kayit okuma ucu yok**: duzenleme modunda kayit
 * `GET /departments` yanitindan (store) cozulur. Ad otel icinde unique
 * oldugundan 409 `name` alanina baglanir.
 */
@Component({
  selector: 'hc-department-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, PageHeader, Button, Spinner],
  templateUrl: './department-form.html',
})
export class DepartmentFormPage {
  private readonly api = inject(DepartmentsApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly store = inject(DepartmentsStore);

  /** Rota parametresi (`withComponentInputBinding`); olusturma modunda bostur. */
  readonly id = input<string | undefined>(undefined);

  protected readonly limits = DEPARTMENT_LIMITS;
  protected readonly validationParams = {
    nameMaxLength: DEPARTMENT_LIMITS.nameMaxLength,
    descriptionMaxLength: DEPARTMENT_LIMITS.descriptionMaxLength,
  };

  protected readonly loading = this.store.loading;
  protected readonly loadError = this.store.error;
  protected readonly saving = signal(false);
  protected readonly notFound = signal(false);
  protected readonly formErrorKey = signal<string | null>(null);
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly submitted = signal(false);

  protected readonly isEdit = computed(() => Boolean(this.id()));

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(DEPARTMENT_LIMITS.nameMaxLength)]],
    description: ['', [Validators.maxLength(DEPARTMENT_LIMITS.descriptionMaxLength)]],
  });

  constructor() {
    void this.store.load();

    // Liste yuklendikten sonra duzenlenen kayit forma yazilir.
    effect(() => {
      const id = this.id();
      const items = this.store.items();
      if (!id || this.store.loading()) {
        return;
      }
      const department = items.find((item) => item.id === id) ?? null;
      this.notFound.set(department === null);
      if (department) {
        this.form.reset({
          name: department.name,
          description: department.description ?? '',
        });
        this.submitted.set(false);
      }
    });
  }

  protected reload(): void {
    void this.store.load(true);
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
    const request: DepartmentWriteRequest = {
      name: raw.name.trim(),
      description: raw.description.trim() || null,
    };

    this.saving.set(true);
    try {
      const id = this.id();
      if (id) {
        await firstValueFrom(this.api.update(id, request));
      } else {
        await firstValueFrom(this.api.create(request));
      }
      // Liste ekrani acilista tazeler; yine de onbellek bayat isaretlenir.
      this.store.invalidate();
      await this.router.navigate(['/employees/departments']);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(['/employees/departments']);
  }

  protected errorKeyFor(controlName: DepartmentFormControl): string | null {
    const control = this.form.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (typeof errors['conflict'] === 'string') {
      return errors['conflict'];
    }
    if (errors['required']) {
      return 'employees.departments.form.validation.nameRequired';
    }
    if (errors['maxlength']) {
      return controlName === 'name'
        ? 'employees.departments.form.validation.nameLength'
        : 'employees.departments.form.validation.descriptionLength';
    }
    return null;
  }

  protected serverMessagesFor(controlName: DepartmentFormControl): readonly string[] {
    return serverErrorMessages(this.form.get(controlName));
  }

  private handleWriteError(error: ApiError): void {
    if (error.status === 409) {
      setServerError(this.form.controls.name, 'employees.departments.form.validation.nameConflict');
      this.formErrorKey.set('employees.departments.form.conflict');
      return;
    }

    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
