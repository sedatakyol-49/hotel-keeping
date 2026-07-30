import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { RatePlansApi } from '../../core/api/rate-plans.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import { RATE_PLAN_LIMITS, type RatePlanWriteRequest } from '../../core/models/rate-plan.model';
import {
  RESERVATION_CHANNELS,
  RESERVATION_CHANNEL_LABEL_KEYS,
  isReservationChannel,
} from '../../core/models/reservation.model';
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
import { RoomTypesStore } from '../rooms/room-types.store';

type RatePlanFormControl = 'roomTypeId' | 'name' | 'price' | 'validFrom' | 'validTo' | 'channel';

/**
 * Fiyat plani olusturma/duzenleme (`POST|PUT /rate-plans`).
 *
 * Aralik **kapali**dir: `validTo` dahil edilir ve tek gunluk plan gecerlidir
 * (bu yuzden `validTo >= validFrom`, `>` degil).
 *
 * **409 cakismasi**: ayni `(roomTypeId, channel)` icin tarih araligi kesisen
 * ikinci **aktif** plan reddedilir. Sunucu `detail` metninde cakisan planin
 * adini ve araligini verir; bu metin oldugu gibi gosterilir ve hata tarih
 * alanlarina baglanir.
 */
@Component({
  selector: 'hc-rate-plan-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, PageHeader, Button, Spinner],
  templateUrl: './rate-plan-form.html',
})
export class RatePlanFormPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(RatePlansApi);

  protected readonly roomTypes = inject(RoomTypesStore);

  protected readonly limits = RATE_PLAN_LIMITS;
  protected readonly channels = RESERVATION_CHANNELS;
  protected readonly channelLabelKeys = RESERVATION_CHANNEL_LABEL_KEYS;

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly formErrorKey = signal<string | null>(null);
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly serverDetail = signal<string | null>(null);
  protected readonly submitted = signal(false);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  protected readonly planId = computed(() => this.params().get('id'));
  protected readonly isEdit = computed(() => this.planId() !== null);

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      roomTypeId: ['', [Validators.required]],
      name: ['', [Validators.required, Validators.maxLength(RATE_PLAN_LIMITS.nameMaxLength)]],
      price: [
        '',
        [
          Validators.required,
          decimalRangeValidator({ min: RATE_PLAN_LIMITS.priceMin, max: RATE_PLAN_LIMITS.priceMax }),
        ],
      ],
      validFrom: ['', [Validators.required, isoDateValidator()]],
      validTo: ['', [Validators.required, isoDateValidator()]],
      channel: [''],
      isActive: [true],
    },
    // Kapali aralik: `validTo === validFrom` gecerlidir (tek gunluk plan).
    { validators: [dateOrderValidator('validFrom', 'validTo', 'validityOrder')] },
  );

  /**
   * Bilincli olarak **metot**tur, `computed()` degil: `form.errors` bir signal
   * degildir; `computed` ilk degerini onbellege alir ve dogrulama sonrasi
   * guncellenmezdi.
   */
  protected validityOrderError(): boolean {
    return this.form.errors?.['validityOrder'] === true;
  }

  constructor() {
    void this.roomTypes.load();
    const id = this.planId();
    if (id) {
      void this.loadPlan(id);
    }
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
    const request: RatePlanWriteRequest = {
      roomTypeId: raw.roomTypeId,
      name: raw.name.trim(),
      // `price` metin kontroludur (de-DE virgullu yazim); tek noktada cozulur.
      price: parseDecimal(raw.price) ?? 0,
      validFrom: raw.validFrom,
      validTo: raw.validTo,
      channel: isReservationChannel(raw.channel) ? raw.channel : null,
      isActive: raw.isActive,
    };

    this.saving.set(true);
    try {
      const id = this.planId();
      if (id) {
        await firstValueFrom(this.api.update(id, request));
      } else {
        await firstValueFrom(this.api.create(request));
      }
      await this.router.navigate(['/reservations/rate-plans']);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(['/reservations/rate-plans']);
  }

  protected errorKeyFor(controlName: RatePlanFormControl): string | null {
    const control = this.form.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (typeof errors['conflict'] === 'string') {
      return errors['conflict'];
    }
    if (errors['required']) {
      return `ratePlans.form.validation.${controlName}Required`;
    }
    if (errors['dateFormat']) {
      return 'ratePlans.form.validation.dateFormat';
    }
    if (errors['decimalFormat'] || errors['decimalRange']) {
      return 'ratePlans.form.validation.price';
    }
    if (errors['maxlength']) {
      return 'ratePlans.form.validation.nameLength';
    }
    return null;
  }

  protected serverMessagesFor(controlName: RatePlanFormControl): readonly string[] {
    return serverErrorMessages(this.form.get(controlName));
  }

  private async loadPlan(id: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const plan = await firstValueFrom(this.api.getById(id));
      this.form.patchValue({
        roomTypeId: plan.roomTypeId,
        name: plan.name,
        price: String(plan.price),
        validFrom: plan.validFrom,
        validTo: plan.validTo,
        channel: plan.channel ?? '',
        isActive: plan.isActive,
      });
    } catch (error: unknown) {
      this.loadError.set(toApiError(error));
    } finally {
      this.loading.set(false);
    }
  }

  private handleWriteError(error: ApiError): void {
    this.serverDetail.set(error.detail ?? null);

    if (error.status === 409) {
      // Cakisma tarih araligindan kaynaklanir; mesaj tarih alanina baglanir.
      setServerError(this.form.controls.validFrom, 'ratePlans.form.validation.overlap');
      this.formErrorKey.set('ratePlans.form.overlap');
      return;
    }

    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
