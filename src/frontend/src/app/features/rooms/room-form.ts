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

import { RoomsApi } from '../../core/api/rooms.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  HOUSEKEEPING_STATUSES,
  HOUSEKEEPING_STATUS_LABEL_KEYS,
  ROOM_LIMITS,
  type HousekeepingStatus,
  type RoomWriteRequest,
} from '../../core/models/room.model';
import {
  applyApiFieldErrors,
  serverErrorMessages,
  setServerError,
} from '../../shared/forms/api-field-errors';
import { integerRangeValidator, parseInteger } from '../../shared/forms/numeric-validators';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { RoomTypesStore } from './room-types.store';

type RoomFormControl = 'number' | 'floor' | 'roomTypeId' | 'housekeepingStatus' | 'note';

/**
 * Oda olustur/duzenle (`POST /rooms`, `PUT /rooms/{id}`).
 *
 * Dogrulama kurallari sozlesmedeki degerlerle istemcide de uygulanir; yine de
 * son soz backend'dedir: 400 yanitindaki `errors` sozlugu ilgili alanlara,
 * 409 (numara cakismasi) `number` alanina baglanir.
 */
@Component({
  selector: 'hc-room-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, PageHeader, Button, Spinner],
  templateUrl: './room-form.html',
})
export class RoomFormPage {
  private readonly api = inject(RoomsApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly roomTypes = inject(RoomTypesStore);

  /** Rota parametresi (`withComponentInputBinding`); olusturma modunda bostur. */
  readonly id = input<string | undefined>(undefined);

  protected readonly statuses = HOUSEKEEPING_STATUSES;
  protected readonly statusLabelKeys = HOUSEKEEPING_STATUS_LABEL_KEYS;
  protected readonly limits = ROOM_LIMITS;
  /** Dogrulama mesajlarinda kullanilan ortak interpolasyon parametreleri. */
  protected readonly validationParams = {
    min: ROOM_LIMITS.floorMin,
    max: ROOM_LIMITS.floorMax,
    maxLength: ROOM_LIMITS.numberMaxLength,
    noteMaxLength: ROOM_LIMITS.noteMaxLength,
  };

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly formErrorKey = signal<string | null>(null);
  /** Alanla eslesmeyen sunucu mesajlari (backend tarafinda cevrilmis gelir). */
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly submitted = signal(false);

  protected readonly isEdit = computed(() => Boolean(this.id()));

  protected readonly form = this.formBuilder.nonNullable.group({
    number: [
      '',
      [
        Validators.required,
        Validators.minLength(ROOM_LIMITS.numberMinLength),
        Validators.maxLength(ROOM_LIMITS.numberMaxLength),
      ],
    ],
    floor: [
      '',
      [Validators.required, integerRangeValidator(ROOM_LIMITS.floorMin, ROOM_LIMITS.floorMax)],
    ],
    roomTypeId: ['', [Validators.required]],
    housekeepingStatus: ['Clean' as HousekeepingStatus, [Validators.required]],
    note: ['', [Validators.maxLength(ROOM_LIMITS.noteMaxLength)]],
  });

  constructor() {
    void this.roomTypes.load();

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
    const status = raw.housekeepingStatus;
    const request: RoomWriteRequest = {
      number: raw.number.trim(),
      floor: parseInteger(raw.floor) ?? 0,
      roomTypeId: raw.roomTypeId,
      housekeepingStatus: status,
      // Sozlesme: `isOutOfOrder` ile durum tutarli tutulur.
      isOutOfOrder: status === 'OutOfOrder',
      note: raw.note.trim() || null,
    };

    this.saving.set(true);
    try {
      const id = this.id();
      if (id) {
        await firstValueFrom(this.api.update(id, request));
      } else {
        await firstValueFrom(this.api.create(request));
      }
      // Listeye donuldugunde `RoomListPage` sorguyu yeniden calistirir.
      await this.router.navigate(['/rooms']);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(['/rooms']);
  }

  /** Validator hatasi -> i18n anahtari (`conflict` hatasi anahtarin kendisini tasir). */
  protected errorKeyFor(controlName: RoomFormControl): string | null {
    const control = this.form.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (typeof errors['conflict'] === 'string') {
      return errors['conflict'];
    }
    if (errors['required']) {
      return `rooms.form.validation.${controlName}Required`;
    }
    if (errors['minlength'] || errors['maxlength']) {
      return controlName === 'note'
        ? 'rooms.form.validation.noteLength'
        : 'rooms.form.validation.numberLength';
    }
    if (errors['integerFormat']) {
      return 'rooms.form.validation.floorFormat';
    }
    if (errors['integerRange']) {
      return 'rooms.form.validation.floorRange';
    }
    return null;
  }

  protected serverMessagesFor(controlName: RoomFormControl): readonly string[] {
    return serverErrorMessages(this.form.get(controlName));
  }

  private async fetch(id: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const room = await firstValueFrom(this.api.getById(id));
      this.form.reset({
        number: room.number,
        floor: String(room.floor),
        roomTypeId: room.roomTypeId,
        housekeepingStatus: room.housekeepingStatus,
        note: room.note ?? '',
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
      setServerError(this.form.controls.number, 'rooms.form.validation.numberConflict');
      this.formErrorKey.set('rooms.form.conflict');
      return;
    }

    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
