import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { RoomTypesApi } from '../../core/api/room-types.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import {
  DEFAULT_LANGUAGE,
  SUPPORTED_LANGUAGES,
  type AppLanguage,
} from '../../core/models/language.model';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  ROOM_TYPE_LIMITS,
  type RoomTypeTranslation,
  type RoomTypeWriteRequest,
} from '../../core/models/room-type.model';
import { CurrentHotelService } from '../../core/services/current-hotel.service';
import {
  applyApiFieldErrors,
  serverErrorMessages,
  setServerError,
} from '../../shared/forms/api-field-errors';
import {
  decimalRangeValidator,
  integerRangeValidator,
  parseDecimal,
  parseInteger,
} from '../../shared/forms/numeric-validators';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { RoomTypesStore } from './room-types.store';

/** `"wifi, minibar , wifi"` -> `['wifi','minibar']`. */
export function parseAmenities(value: string): readonly string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const raw of value.split(',')) {
    const amenity = raw.trim();
    const key = amenity.toLocaleLowerCase();
    if (amenity && !seen.has(key)) {
      seen.add(key);
      result.push(amenity);
    }
  }
  return result;
}

/** Donanim listesi sinirlari (backend `AmenityList` ile ayni degerler). */
function amenitiesValidator(control: AbstractControl): ValidationErrors | null {
  const amenities = parseAmenities(String(control.value ?? ''));
  if (amenities.length > ROOM_TYPE_LIMITS.amenityMaxCount) {
    return { amenityLimit: { max: ROOM_TYPE_LIMITS.amenityMaxCount } };
  }
  if (amenities.some((amenity) => amenity.length > ROOM_TYPE_LIMITS.amenityMaxLength)) {
    return { amenityLimit: { max: ROOM_TYPE_LIMITS.amenityMaxLength } };
  }
  return null;
}

/**
 * Oda tipi olustur/duzenle (`POST`/`PUT /room-types`).
 *
 * Ad ve aciklama **cok dillidir**: DE/EN/TR sekmeleri `translations` alanini
 * doldurur. DE birincil dildir; entity'nin varsayilan (fallback) degeri de
 * DE sekmesinden alinir (sozlesme: ceviri yoksa varsayilan degere duser).
 */
@Component({
  selector: 'hc-room-type-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, MoneyPipe, PageHeader, Button, Spinner],
  templateUrl: './room-type-form.html',
})
export class RoomTypeFormPage {
  private readonly api = inject(RoomTypesApi);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly store = inject(RoomTypesStore);
  private readonly currentHotel = inject(CurrentHotelService);

  /** Rota parametresi (`withComponentInputBinding`). */
  readonly id = input<string | undefined>(undefined);

  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly defaultLanguage = DEFAULT_LANGUAGE;
  protected readonly limits = ROOM_TYPE_LIMITS;
  protected readonly validationParams = {
    maxLength: ROOM_TYPE_LIMITS.codeMaxLength,
    capacityMin: ROOM_TYPE_LIMITS.capacityMin,
    capacityMax: ROOM_TYPE_LIMITS.capacityMax,
    priceMin: ROOM_TYPE_LIMITS.basePriceMin,
    nameMaxLength: ROOM_TYPE_LIMITS.nameMaxLength,
    descriptionMaxLength: ROOM_TYPE_LIMITS.descriptionMaxLength,
    amenityMaxCount: ROOM_TYPE_LIMITS.amenityMaxCount,
    amenityMaxLength: ROOM_TYPE_LIMITS.amenityMaxLength,
  };

  protected readonly activeLanguage = signal<AppLanguage>(DEFAULT_LANGUAGE);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly formErrorKey = signal<string | null>(null);
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly submitted = signal(false);
  /** Duzenlemede kaydin kendi para birimi, olusturmada aktif otelin birimi. */
  private readonly loadedCurrency = signal<string | null>(null);

  protected readonly isEdit = computed(() => Boolean(this.id()));
  protected readonly currency = computed(
    () => this.loadedCurrency() ?? this.currentHotel.hotel()?.currency ?? 'EUR',
  );

  protected readonly form = this.formBuilder.nonNullable.group({
    code: [
      '',
      [
        Validators.required,
        Validators.minLength(ROOM_TYPE_LIMITS.codeMinLength),
        Validators.maxLength(ROOM_TYPE_LIMITS.codeMaxLength),
      ],
    ],
    basePrice: [
      '',
      [Validators.required, decimalRangeValidator({ min: ROOM_TYPE_LIMITS.basePriceMin })],
    ],
    capacity: [
      '',
      [
        Validators.required,
        integerRangeValidator(ROOM_TYPE_LIMITS.capacityMin, ROOM_TYPE_LIMITS.capacityMax),
      ],
    ],
    // `sizeSqm` backend'de tam sayidir (`int?`), bu yuzden ondalik kabul edilmez.
    sizeSqm: ['', [integerRangeValidator(ROOM_TYPE_LIMITS.sizeSqmMin, Number.MAX_SAFE_INTEGER)]],
    amenities: ['', [amenitiesValidator]],
    translations: this.formBuilder.nonNullable.group({
      de: this.formBuilder.nonNullable.group({
        name: ['', [Validators.required, Validators.maxLength(ROOM_TYPE_LIMITS.nameMaxLength)]],
        description: ['', [Validators.maxLength(ROOM_TYPE_LIMITS.descriptionMaxLength)]],
      }),
      en: this.formBuilder.nonNullable.group({
        name: ['', [Validators.maxLength(ROOM_TYPE_LIMITS.nameMaxLength)]],
        description: ['', [Validators.maxLength(ROOM_TYPE_LIMITS.descriptionMaxLength)]],
      }),
      tr: this.formBuilder.nonNullable.group({
        name: ['', [Validators.maxLength(ROOM_TYPE_LIMITS.nameMaxLength)]],
        description: ['', [Validators.maxLength(ROOM_TYPE_LIMITS.descriptionMaxLength)]],
      }),
    }),
  });

  private readonly amenitiesValue = toSignal(this.form.controls.amenities.valueChanges, {
    initialValue: '',
  });
  private readonly basePriceValue = toSignal(this.form.controls.basePrice.valueChanges, {
    initialValue: '',
  });

  protected readonly amenityPreview = computed(() => parseAmenities(this.amenitiesValue()));
  protected readonly pricePreview = computed(() => parseDecimal(this.basePriceValue()));

  constructor() {
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

  /** Sekme etiketinde dolu/bos isareti gostermek icin. */
  protected hasTranslation(language: AppLanguage): boolean {
    const group = this.form.controls.translations.get(language);
    const value = group?.value as RoomTypeTranslation | undefined;
    return Boolean(value?.name?.trim() || value?.description?.trim());
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.formErrorKey.set(null);
    this.formErrors.set([]);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // Zorunlu DE adi eksikse kullanici dogru sekmeye getirilir.
      if (this.form.controls.translations.controls.de.invalid) {
        this.activeLanguage.set(DEFAULT_LANGUAGE);
      }
      return;
    }

    const raw = this.form.getRawValue();
    const translations: Record<string, RoomTypeTranslation> = {};
    for (const language of SUPPORTED_LANGUAGES) {
      const name = raw.translations[language].name.trim();
      const description = raw.translations[language].description.trim();
      if (name || description) {
        translations[language] = { name: name || null, description: description || null };
      }
    }

    const request: RoomTypeWriteRequest = {
      code: raw.code.trim(),
      // Varsayilan (fallback) deger birincil dilden alinir.
      name: raw.translations[DEFAULT_LANGUAGE].name.trim(),
      description: raw.translations[DEFAULT_LANGUAGE].description.trim() || null,
      basePrice: parseDecimal(raw.basePrice) ?? 0,
      capacity: parseInteger(raw.capacity) ?? ROOM_TYPE_LIMITS.capacityMin,
      sizeSqm: raw.sizeSqm.trim() ? parseInteger(raw.sizeSqm) : null,
      amenities: parseAmenities(raw.amenities),
      translations,
    };

    this.saving.set(true);
    try {
      const id = this.id();
      if (id) {
        await firstValueFrom(this.api.update(id, request));
      } else {
        await firstValueFrom(this.api.create(request));
      }
      this.store.invalidate();
      await this.router.navigate(['/rooms/types']);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    void this.router.navigate(['/rooms/types']);
  }

  protected errorKeyFor(path: string, field: string): string | null {
    const control = this.form.get(path);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    const errors = control.errors ?? {};
    if (typeof errors['conflict'] === 'string') {
      return errors['conflict'];
    }
    if (errors['amenityLimit']) {
      return 'rooms.types.validation.amenitiesLimit';
    }
    if (errors['required']) {
      return `rooms.types.validation.${field}Required`;
    }
    if (errors['minlength'] || errors['maxlength']) {
      return `rooms.types.validation.${field}Length`;
    }
    if (errors['integerFormat'] || errors['decimalFormat']) {
      return `rooms.types.validation.${field}Format`;
    }
    if (errors['integerRange'] || errors['decimalRange']) {
      return `rooms.types.validation.${field}Range`;
    }
    return null;
  }

  protected serverMessagesFor(path: string): readonly string[] {
    return serverErrorMessages(this.form.get(path));
  }

  private async fetch(id: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const type = await firstValueFrom(this.api.getById(id));
      this.loadedCurrency.set(type.currency);
      const translations = type.translations ?? {};
      this.form.reset({
        code: type.code,
        basePrice: String(type.basePrice),
        capacity: String(type.capacity),
        sizeSqm: type.sizeSqm === null || type.sizeSqm === undefined ? '' : String(type.sizeSqm),
        amenities: type.amenities.join(', '),
        translations: {
          de: {
            // Ceviri yoksa cozumlenmis deger yalnizca birincil dile yazilir.
            name: translations.de?.name ?? (type.translations ? '' : type.name),
            description:
              translations.de?.description ?? (type.translations ? '' : (type.description ?? '')),
          },
          en: {
            name: translations.en?.name ?? '',
            description: translations.en?.description ?? '',
          },
          tr: {
            name: translations.tr?.name ?? '',
            description: translations.tr?.description ?? '',
          },
        },
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
      setServerError(this.form.controls.code, 'rooms.types.validation.codeConflict');
      this.formErrorKey.set('rooms.types.form.conflict');
      return;
    }

    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
