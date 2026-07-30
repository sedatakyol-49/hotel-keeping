import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { GuestsApi } from '../../core/api/guests.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import { GUEST_LIMITS, type GuestWriteRequest } from '../../core/models/guest.model';
import { SUPPORTED_LANGUAGES, isAppLanguage } from '../../core/models/language.model';
import type { ApiError } from '../../core/models/problem-details.model';
import { COUNTRIES } from '../../core/models/settings.model';
import { applyApiFieldErrors, serverErrorMessages } from '../../shared/forms/api-field-errors';
import { isoDateValidator } from '../../shared/forms/date-validators';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';

type GuestFormControl =
  | 'firstName'
  | 'lastName'
  | 'email'
  | 'phone'
  | 'nationality'
  | 'addressLine'
  | 'postalCode'
  | 'city'
  | 'birthDate'
  | 'culture'
  | 'note';

/**
 * Misafir olusturma/duzenleme (`POST /guests`, `PUT /guests/{id}`).
 *
 * Sozlesme notu: misafirde **benzersizlik kurali yoktur** — ayni isim/e-posta
 * ile birden cok kayit mesrudur (ayni adi tasiyan farkli kisiler, ailenin ortak
 * e-postasi). Bu yuzden form "bu misafir zaten var" gibi bir uyari uretmez ve
 * sessizce mevcut kayda baglanmaz.
 *
 * `stayCount` yalnizca **detay** yanitinda dolu gelir; duzenleme ekraninda
 * salt-okunur bir bilgi olarak gosterilir (tamamlanmis konaklama sayisi).
 */
@Component({
  selector: 'hc-guest-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, PageHeader, Button, Spinner],
  templateUrl: './guest-form.html',
})
export class GuestFormPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(GuestsApi);

  protected readonly limits = GUEST_LIMITS;
  protected readonly countries = COUNTRIES;
  protected readonly cultures = SUPPORTED_LANGUAGES;

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly formErrorKey = signal<string | null>(null);
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly serverDetail = signal<string | null>(null);
  protected readonly submitted = signal(false);
  /** Yalnizca detay yanitinda gelir. */
  protected readonly stayCount = signal<number | null>(null);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  protected readonly guestId = computed(() => this.params().get('id'));
  protected readonly isEdit = computed(() => this.guestId() !== null);

  protected readonly form = this.formBuilder.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(GUEST_LIMITS.firstNameMaxLength)]],
    lastName: ['', [Validators.required, Validators.maxLength(GUEST_LIMITS.lastNameMaxLength)]],
    email: ['', [Validators.email, Validators.maxLength(GUEST_LIMITS.emailMaxLength)]],
    phone: ['', [Validators.maxLength(GUEST_LIMITS.phoneMaxLength)]],
    nationality: [''],
    addressLine: ['', [Validators.maxLength(GUEST_LIMITS.addressLineMaxLength)]],
    postalCode: ['', [Validators.maxLength(GUEST_LIMITS.postalCodeMaxLength)]],
    city: ['', [Validators.maxLength(GUEST_LIMITS.cityMaxLength)]],
    birthDate: ['', [isoDateValidator()]],
    culture: ['de'],
    note: ['', [Validators.maxLength(GUEST_LIMITS.noteMaxLength)]],
  });

  constructor() {
    const id = this.guestId();
    if (id) {
      void this.loadGuest(id);
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
    const request: GuestWriteRequest = {
      firstName: raw.firstName.trim(),
      lastName: raw.lastName.trim(),
      email: raw.email.trim() || null,
      phone: raw.phone.trim() || null,
      nationality: raw.nationality || null,
      addressLine: raw.addressLine.trim() || null,
      postalCode: raw.postalCode.trim() || null,
      city: raw.city.trim() || null,
      birthDate: raw.birthDate || null,
      culture: isAppLanguage(raw.culture) ? raw.culture : null,
      note: raw.note.trim() || null,
    };

    this.saving.set(true);
    try {
      const id = this.guestId();
      if (id) {
        await firstValueFrom(this.api.update(id, request));
      } else {
        await firstValueFrom(this.api.create(request));
      }
      await this.router.navigate(['/reservations/guests']);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(['/reservations/guests']);
  }

  protected errorKeyFor(controlName: GuestFormControl): string | null {
    const control = this.form.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (errors['required']) {
      return `guests.form.validation.${controlName}Required`;
    }
    if (errors['email']) {
      return 'guests.form.validation.email';
    }
    if (errors['dateFormat']) {
      return 'guests.form.validation.dateFormat';
    }
    if (errors['maxlength']) {
      return 'guests.form.validation.tooLong';
    }
    return null;
  }

  protected serverMessagesFor(controlName: GuestFormControl): readonly string[] {
    return serverErrorMessages(this.form.get(controlName));
  }

  private async loadGuest(id: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const guest = await firstValueFrom(this.api.getById(id));
      this.stayCount.set(guest.stayCount ?? null);
      this.form.patchValue({
        firstName: guest.firstName,
        lastName: guest.lastName,
        email: guest.email ?? '',
        phone: guest.phone ?? '',
        nationality: guest.nationality ?? '',
        addressLine: guest.addressLine ?? '',
        postalCode: guest.postalCode ?? '',
        city: guest.city ?? '',
        birthDate: guest.birthDate ?? '',
        culture: guest.culture ?? 'de',
        note: guest.note ?? '',
      });
    } catch (error: unknown) {
      this.loadError.set(toApiError(error));
    } finally {
      this.loading.set(false);
    }
  }

  private handleWriteError(error: ApiError): void {
    this.serverDetail.set(error.detail ?? null);
    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
