import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  type AbstractControl,
  type ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { GuestsApi } from '../../core/api/guests.api';
import { ReservationsApi } from '../../core/api/reservations.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import {
  addDays,
  nightsBetween,
  todayIso,
  type AvailabilityResponse,
  type AvailableRoomResponse,
} from '../../core/models/availability.model';
import { GUEST_LIMITS, type GuestResponse } from '../../core/models/guest.model';
import { SUPPORTED_LANGUAGES } from '@hotelcore/shared';
import type { ApiError } from '../../core/models/problem-details.model';
import {
  RESERVATION_CHANNELS,
  RESERVATION_CHANNEL_LABEL_KEYS,
  RESERVATION_LIMITS,
  isReservationChannel,
  type CreateReservationRequest,
  type ReservationChannel,
  type ReservationResponse,
} from '../../core/models/reservation.model';
import { COUNTRIES } from '../../core/models/settings.model';
import { applyApiFieldErrors, serverErrorMessages } from '../../shared/forms/api-field-errors';
import { isIsoDate, isoDateValidator } from '../../shared/forms/date-validators';
import { parseDecimal, parseInteger } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { RoomTypesStore } from '../rooms/room-types.store';
import { GuestOptionsStore } from './guest-options.store';

/** Sihirbaz adimlari (sirali). */
export const WIZARD_STEPS = ['dates', 'room', 'guest', 'details', 'done'] as const;

export type WizardStep = (typeof WIZARD_STEPS)[number];

/**
 * `to > from` (en az 1 gece) ve en fazla 365 gece.
 * Hata **gruba** islenir; alt kontroller degistirilmez.
 */
function stayRangeValidator(control: AbstractControl): ValidationErrors | null {
  const from = String(control.get('from')?.value ?? '');
  const to = String(control.get('to')?.value ?? '');
  if (!isIsoDate(from) || !isIsoDate(to)) {
    return null;
  }
  const nights = nightsBetween(from, to);
  if (nights === null || nights < 1) {
    // Ayni gun cikis (day-use) bu fazda desteklenmez.
    return { stayTooShort: true };
  }
  return nights > RESERVATION_LIMITS.maxNights ? { stayTooLong: true } : null;
}

/**
 * Rezervasyon sihirbazi (`POST /reservations`).
 *
 * Adimlar: **tarih + oda tipi** → `GET /availability` ile **musait odalar** →
 * oda secimi → **misafir sec veya yeni olustur** → kisi sayisi/kanal/notlar →
 * kayit.
 *
 * ### Tutar hicbir zaman istemciden gitmez
 * `CreateReservationRequest` tipinde `totalAmount` alani **yoktur**: fiyat gece
 * gece sunucuda hesaplanir (kanala ozel plan → tum kanallar plani → oda tipi
 * `basePrice`). Sihirbaz hicbir fiyat tahmini uretmez — musaitlik yanitinda
 * zaten fiyat alani da yoktur. Kayit sonrasi ekran **sunucunun dondurdugu**
 * `totalAmount`, `depositAmount` ve varsa `ratePlanName` degerlerini gosterir.
 *
 * ### Cakisma (409)
 * Sunucu `detail` metninde **hangi rezervasyonla** cakisildigini soyler
 * (numara + tarihler); bu metin oldugu gibi gosterilir ve kullanici oda adimina
 * geri gonderilir — musaitlik listesi bu arada bayatlamis olabilir.
 */
@Component({
  selector: 'hc-reservation-wizard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    MoneyPipe,
    PageHeader,
    Spinner,
    Button,
  ],
  templateUrl: './reservation-wizard.html',
})
export class ReservationWizardPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(ReservationsApi);
  private readonly guestsApi = inject(GuestsApi);

  protected readonly roomTypes = inject(RoomTypesStore);
  protected readonly guests = inject(GuestOptionsStore);

  protected readonly steps = WIZARD_STEPS;
  protected readonly channels = RESERVATION_CHANNELS;
  protected readonly channelLabelKeys = RESERVATION_CHANNEL_LABEL_KEYS;
  protected readonly limits = RESERVATION_LIMITS;
  protected readonly guestLimits = GUEST_LIMITS;
  protected readonly countries = COUNTRIES;
  protected readonly cultures = SUPPORTED_LANGUAGES;

  protected readonly step = signal<WizardStep>('dates');
  protected readonly availability = signal<AvailabilityResponse | null>(null);
  protected readonly availabilityLoading = signal(false);
  protected readonly availabilityError = signal<ApiError | null>(null);
  protected readonly selectedRoom = signal<AvailableRoomResponse | null>(null);
  protected readonly selectedGuest = signal<GuestResponse | null>(null);
  /** Yeni misafir formu acik mi (listeden secmek yerine). */
  protected readonly guestFormOpen = signal(false);
  protected readonly guestSaving = signal(false);
  protected readonly guestError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  protected readonly conflictDetail = signal<string | null>(null);
  protected readonly formErrorKey = signal<string | null>(null);
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly created = signal<ReservationResponse | null>(null);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  /** Doluluk izgarasindaki bos geceden gelen on-doldurma. */
  private readonly prefill = computed(() => {
    const params = this.queryParams();
    const from = params.get('from');
    const to = params.get('to');
    return {
      roomId: params.get('roomId'),
      from: from && isIsoDate(from) ? from : todayIso(),
      to: to && isIsoDate(to) ? to : addDays(todayIso(), 1),
    };
  });

  protected readonly datesForm = this.formBuilder.nonNullable.group(
    {
      from: ['', [Validators.required, isoDateValidator()]],
      to: ['', [Validators.required, isoDateValidator()]],
      roomTypeId: [''],
    },
    { validators: [stayRangeValidator] },
  );

  protected readonly detailsForm = this.formBuilder.nonNullable.group({
    adults: [2, [Validators.required]],
    children: [0],
    channel: ['Direct' as ReservationChannel, [Validators.required]],
    depositPercent: [0],
    status: ['Confirmed' as 'Option' | 'Confirmed', [Validators.required]],
    notes: ['', [Validators.maxLength(RESERVATION_LIMITS.notesMaxLength)]],
  });

  protected readonly newGuestForm = this.formBuilder.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(GUEST_LIMITS.firstNameMaxLength)]],
    lastName: ['', [Validators.required, Validators.maxLength(GUEST_LIMITS.lastNameMaxLength)]],
    email: ['', [Validators.email, Validators.maxLength(GUEST_LIMITS.emailMaxLength)]],
    phone: ['', [Validators.maxLength(GUEST_LIMITS.phoneMaxLength)]],
    nationality: [''],
    culture: ['de'],
  });

  private readonly datesValue = toSignal(this.datesForm.valueChanges, {
    initialValue: this.datesForm.getRawValue(),
  });
  private readonly detailsValue = toSignal(this.detailsForm.valueChanges, {
    initialValue: this.detailsForm.getRawValue(),
  });

  /** Gece sayisi onizlemesi; tutar **gosterilmez** (yalnizca sunucu hesaplar). */
  protected readonly nights = computed(() => {
    const value = this.datesValue();
    if (!isIsoDate(value.from ?? '') || !isIsoDate(value.to ?? '')) {
      return null;
    }
    return nightsBetween(value.from!, value.to!);
  });

  protected readonly stayTooShort = computed(() => {
    const nights = this.nights();
    return nights !== null && nights < 1;
  });
  protected readonly stayTooLong = computed(() => {
    const nights = this.nights();
    return nights !== null && nights > RESERVATION_LIMITS.maxNights;
  });

  /**
   * `adults + children` oda kapasitesini asamaz (sunucu 400 doner).
   * Musaitlik yaniti kapasiteyi verdigi icin istemci bunu onceden gorebilir.
   */
  protected readonly capacityExceeded = computed(() => {
    const room = this.selectedRoom();
    if (!room) {
      return false;
    }
    const value = this.detailsValue();
    const people = (parseInteger(value.adults) ?? 0) + (parseInteger(value.children) ?? 0);
    return people > room.capacity;
  });

  protected readonly canSubmit = computed(
    () => this.selectedRoom() !== null && this.selectedGuest() !== null && !this.capacityExceeded(),
  );

  constructor() {
    void this.roomTypes.load();
    void this.guests.load();

    const prefill = this.prefill();
    this.datesForm.patchValue({ from: prefill.from, to: prefill.to });
  }

  // --- Adim 1: tarih + oda tipi -------------------------------------------

  /** `GET /availability` — musait odalar; **fiyat alani yoktur**. */
  protected async searchAvailability(): Promise<void> {
    if (this.datesForm.invalid) {
      this.datesForm.markAllAsTouched();
      return;
    }

    const raw = this.datesForm.getRawValue();
    this.availabilityLoading.set(true);
    this.availabilityError.set(null);
    this.selectedRoom.set(null);

    try {
      const response = await firstValueFrom(
        this.api.availability({
          from: raw.from,
          to: raw.to,
          roomTypeId: raw.roomTypeId || null,
        }),
      );
      this.availability.set(response);
      this.step.set('room');

      // On-doldurulmus oda hala musaitse otomatik secilir (izgaradan gelis).
      const prefilledRoomId = this.prefill().roomId;
      const prefilled = prefilledRoomId
        ? (response.rooms.find((room) => room.roomId === prefilledRoomId) ?? null)
        : null;
      if (prefilled) {
        this.selectRoom(prefilled);
      }
    } catch (error: unknown) {
      this.availability.set(null);
      this.availabilityError.set(toApiError(error));
    } finally {
      this.availabilityLoading.set(false);
    }
  }

  // --- Adim 2: oda secimi --------------------------------------------------

  protected selectRoom(room: AvailableRoomResponse): void {
    this.selectedRoom.set(room);
    this.conflictDetail.set(null);
  }

  protected confirmRoom(): void {
    if (this.selectedRoom()) {
      this.step.set('guest');
    }
  }

  // --- Adim 3: misafir -----------------------------------------------------

  protected searchGuests(term: string): void {
    void this.guests.load(term);
  }

  protected selectGuest(guest: GuestResponse): void {
    this.selectedGuest.set(guest);
    this.guestFormOpen.set(false);
  }

  protected openGuestForm(): void {
    this.guestError.set(null);
    this.guestFormOpen.set(true);
  }

  protected closeGuestForm(): void {
    this.guestFormOpen.set(false);
  }

  /** `POST /guests` — yeni misafir olusturulur ve dogrudan secilir. */
  protected async createGuest(): Promise<void> {
    if (this.newGuestForm.invalid) {
      this.newGuestForm.markAllAsTouched();
      return;
    }

    const raw = this.newGuestForm.getRawValue();
    this.guestSaving.set(true);
    this.guestError.set(null);

    try {
      const guest = await firstValueFrom(
        this.guestsApi.create({
          firstName: raw.firstName.trim(),
          lastName: raw.lastName.trim(),
          email: raw.email.trim() || null,
          phone: raw.phone.trim() || null,
          nationality: raw.nationality || null,
          culture: raw.culture === 'en' || raw.culture === 'tr' ? raw.culture : 'de',
        }),
      );
      this.guests.prepend(guest);
      this.selectGuest(guest);
      this.newGuestForm.reset({ culture: 'de' });
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this.guestError.set(apiError);
      applyApiFieldErrors(this.newGuestForm, apiError);
    } finally {
      this.guestSaving.set(false);
    }
  }

  protected confirmGuest(): void {
    if (this.selectedGuest()) {
      this.step.set('details');
    }
  }

  // --- Adim 4: detaylar + kayit -------------------------------------------

  /**
   * `POST /reservations`.
   *
   * Gonderilen govdede **`totalAmount` yoktur**; sunucu tutari gece gece
   * hesaplar ve yanitta dondurur.
   */
  protected async submit(): Promise<void> {
    const room = this.selectedRoom();
    const guest = this.selectedGuest();
    if (!room || !guest || this.detailsForm.invalid || this.capacityExceeded()) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    const dates = this.datesForm.getRawValue();
    const raw = this.detailsForm.getRawValue();

    // Sayisal alanlar: `type="number"` + `formControlName` ikilisinde Angular
    // kontrole **sayi** yazar; `parseInteger`/`parseDecimal` her iki yazimi da
    // kabul eder (`.trim()` cagrilmaz — sayida metot yoktur).
    const request: CreateReservationRequest = {
      roomId: room.roomId,
      guestId: guest.id,
      checkIn: dates.from,
      checkOut: dates.to,
      adults: parseInteger(raw.adults) ?? RESERVATION_LIMITS.adultsMin,
      children: parseInteger(raw.children) ?? 0,
      channel: isReservationChannel(raw.channel) ? raw.channel : 'Direct',
      depositPercent: parseDecimal(raw.depositPercent) ?? 0,
      notes: String(raw.notes ?? '').trim() || null,
      status: raw.status === 'Option' ? 'Option' : 'Confirmed',
    };

    this.saving.set(true);
    this.conflictDetail.set(null);
    this.formErrorKey.set(null);
    this.formErrors.set([]);

    try {
      const created = await firstValueFrom(this.api.create(request));
      this.created.set(created);
      this.step.set('done');
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected goToStep(step: WizardStep): void {
    // Ileri atlamak yok: yalnizca tamamlanmis adima donulebilir.
    if (this.stepIndex(step) < this.stepIndex(this.step())) {
      this.step.set(step);
    }
  }

  protected stepIndex(step: WizardStep): number {
    return WIZARD_STEPS.indexOf(step);
  }

  protected serverMessagesFor(controlName: string): readonly string[] {
    return serverErrorMessages(this.newGuestForm.get(controlName));
  }

  protected cancel(): void {
    void this.router.navigate(['/reservations']);
  }

  private handleWriteError(error: ApiError): void {
    if (error.status === 409) {
      // Sunucu hangi rezervasyonla cakisildigini `detail`'de soyler; oda adimina
      // donulur cunku musaitlik listesi bayatlamis olabilir.
      this.conflictDetail.set(error.detail ?? null);
      this.formErrorKey.set('reservations.wizard.conflict');
      this.step.set('room');
      void this.searchAvailability();
      return;
    }

    const unmatched = applyApiFieldErrors(this.detailsForm, error);
    this.formErrors.set(unmatched);
    this.conflictDetail.set(error.detail ?? null);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    } else {
      this.formErrorKey.set('errors.validation');
    }
  }
}
