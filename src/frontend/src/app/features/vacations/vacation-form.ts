import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { VacationsApi } from '../../core/api/vacations.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import { VACATION_LIMITS, type CreateVacationRequest } from '../../core/models/vacation.model';
import {
  applyApiFieldErrors,
  serverErrorMessages,
  setServerError,
} from '../../shared/forms/api-field-errors';
import {
  dateOrderValidator,
  isIsoDate,
  isoDateValidator,
} from '../../shared/forms/date-validators';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { EmployeeOptionsStore } from '../employees/employee-options.store';

type VacationFormControl = 'employeeId' | 'from' | 'to' | 'reason';

/** Iki tarih arasindaki **takvim** gunu (her iki uc dahil). */
export function calendarDaysBetween(from: string, to: string): number | null {
  if (!isIsoDate(from) || !isIsoDate(to)) {
    return null;
  }
  const start = Date.parse(`${from}T00:00:00Z`);
  const end = Date.parse(`${to}T00:00:00Z`);
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) {
    return null;
  }
  return Math.round((end - start) / 86_400_000) + 1;
}

/**
 * Yeni izin talebi (`POST /vacations`).
 *
 * `to >= from` kurali istemcide de uygulanir (grup seviyesinde), boylece
 * sunucuya bos yere istek gitmez. **409** cakismasi ("bu tarihlerde bekleyen
 * veya onaylanmis baska talep var") anlamli bir mesaja cevrilir ve tarih
 * alanlarina baglanir; sunucunun `detail` metni de ayrica gosterilir.
 *
 * Gun sayisi onizlemesi **takvim gunudur** (sozlesme: hafta sonu/tatil
 * hesabi yok) ve yalnizca bilgilendirme amaclidir — kaydedilen deger
 * sunucunun hesapladigi `requestedDays` degeridir.
 */
@Component({
  selector: 'hc-vacation-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, PageHeader, Button, Spinner],
  templateUrl: './vacation-form.html',
})
export class VacationFormPage {
  private readonly api = inject(VacationsApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly employees = inject(EmployeeOptionsStore);

  protected readonly limits = VACATION_LIMITS;
  protected readonly validationParams = {
    reasonMaxLength: VACATION_LIMITS.reasonMaxLength,
  };

  protected readonly saving = signal(false);
  protected readonly formErrorKey = signal<string | null>(null);
  /** Alanla eslesmeyen sunucu mesajlari (backend tarafinda cevrilmis gelir). */
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly serverDetail = signal<string | null>(null);
  protected readonly submitted = signal(false);

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      employeeId: ['', [Validators.required]],
      from: ['', [Validators.required, isoDateValidator()]],
      to: ['', [Validators.required, isoDateValidator()]],
      reason: ['', [Validators.maxLength(VACATION_LIMITS.reasonMaxLength)]],
    },
    { validators: [dateOrderValidator('from', 'to', 'periodOrder')] },
  );

  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  /** Takvim gunu onizlemesi; taraflar gecerli degilse gosterilmez. */
  protected readonly daysPreview = computed(() => {
    const value = this.formValue();
    return calendarDaysBetween(value.from ?? '', value.to ?? '');
  });

  /**
   * `to < from`: `dateOrderValidator` hatayi **gruba** isler (alt kontrolleri
   * degistirmez), bu yuzden mesaj burada degerlerden turetilir ve `to` alaninin
   * yaninda gosterilir.
   */
  protected readonly periodOrderError = computed(() => {
    const { from, to } = this.formValue();
    return Boolean(from && to && isIsoDate(from) && isIsoDate(to) && to < from);
  });

  constructor() {
    void this.employees.load();
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.formErrorKey.set(null);
    this.formErrors.set([]);
    this.serverDetail.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const request: CreateVacationRequest = {
      employeeId: raw.employeeId,
      from: raw.from,
      to: raw.to,
      reason: raw.reason.trim() || null,
    };

    this.saving.set(true);
    try {
      await firstValueFrom(this.api.create(request));
      // Listeye donuldugunde `VacationListPage` sorguyu ve bakiyeyi yeniden calistirir.
      await this.router.navigate(['/vacations']);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(['/vacations']);
  }

  /** Validator hatasi -> i18n anahtari (`conflict` hatasi anahtarin kendisini tasir). */
  protected errorKeyFor(controlName: VacationFormControl): string | null {
    const control = this.form.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (typeof errors['conflict'] === 'string') {
      return errors['conflict'];
    }
    if (errors['required']) {
      return `vacations.form.validation.${controlName}Required`;
    }
    if (errors['dateFormat']) {
      return 'vacations.form.validation.dateFormat';
    }
    if (errors['maxlength']) {
      return 'vacations.form.validation.reasonLength';
    }
    return null;
  }

  protected serverMessagesFor(controlName: VacationFormControl): readonly string[] {
    return serverErrorMessages(this.form.get(controlName));
  }

  private handleWriteError(error: ApiError): void {
    this.serverDetail.set(error.detail ?? null);

    if (error.status === 409) {
      // Cakisma tarih araligindan kaynaklanir: mesaj tarih alanlarina baglanir.
      setServerError(this.form.controls.from, 'vacations.form.validation.overlap');
      this.formErrorKey.set('vacations.form.overlap');
      return;
    }

    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
