import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  type FormArray,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { InvoicesApi } from '../../core/api/invoices.api';
import { ReservationsApi } from '../../core/api/reservations.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import {
  INVOICE_LIMITS,
  INVOICE_LINE_TYPES,
  INVOICE_LINE_TYPE_LABEL_KEYS,
  isEditableInvoice,
  isInvoiceLineType,
  type CreateInvoiceRequest,
  type InvoiceLineItemRequest,
  type InvoiceStatus,
  type UpdateInvoiceRequest,
} from '../../core/models/invoice.model';
import { SUPPORTED_LANGUAGES, isAppLanguage } from '@hotelcore/shared';
import type { ApiError } from '../../core/models/problem-details.model';
import type { ReservationResponse } from '../../core/models/reservation.model';
import { applyApiFieldErrors } from '../../shared/forms/api-field-errors';
import { parseDecimal } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { GuestOptionsStore } from '../reservations/guest-options.store';

/** Olusturma yolu: rezervasyondan uret veya elle satir gir. */
export type InvoiceSource = 'reservation' | 'manual';

/**
 * Fatura taslagi olusturma/duzenleme (`POST|PUT /invoices`).
 *
 * ### Iki yol birbirini disler
 * - **Rezervasyondan**: govde yalnizca `{ reservationId, culture }`. Satirlari
 *   **sunucu** uretir (oda ucreti + faturalanmamis folio satirlari + Kurtaxe);
 *   `lineItems` gonderilirse sunucu 400 doner, bu yuzden gonderilmez.
 * - **Elle**: `{ guestId, culture, lineItems }`.
 *
 * ### Istemci tutar hesaplamaz
 * Satirda yalnizca `type`, `description`, `quantity`, `unitPrice` (**brut**) ve
 * `serviceDate` gonderilir. `vatRate`, `lineNet`, `lineVat` ve fatura toplami
 * yazma sozlesmesinde **yoktur** — vergi matrahi manipule edilemez. Ekrandaki
 * satir toplami yalnizca `quantity × unitPrice` onizlemesidir ve bunu boyle
 * soyler.
 *
 * ### Yalnizca taslak duzenlenebilir
 * Duzenleme modunda fatura `Draft` degilse form hic gosterilmez (GoBD §6.1).
 * Bu ikinci savunma hattidir; detay ekrani zaten duzenleme baglantisini
 * kesinlesmis faturada hic render etmez.
 */
@Component({
  selector: 'hc-invoice-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    MoneyPipe,
    PageHeader,
    Button,
    Spinner,
  ],
  templateUrl: './invoice-form.html',
})
export class InvoiceFormPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private readonly api = inject(InvoicesApi);
  private readonly reservationsApi = inject(ReservationsApi);

  protected readonly guests = inject(GuestOptionsStore);

  protected readonly lineTypes = INVOICE_LINE_TYPES;
  protected readonly lineTypeLabelKeys = INVOICE_LINE_TYPE_LABEL_KEYS;
  protected readonly cultures = SUPPORTED_LANGUAGES;
  protected readonly limits = INVOICE_LIMITS;

  protected readonly source = signal<InvoiceSource>('manual');
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly formErrorKey = signal<string | null>(null);
  protected readonly formErrors = signal<readonly string[]>([]);
  protected readonly serverDetail = signal<string | null>(null);
  protected readonly submitted = signal(false);
  /** Duzenleme modunda faturanin durumu — `Draft` degilse form kilitli. */
  protected readonly editStatus = signal<InvoiceStatus | null>(null);

  protected readonly selectedGuestId = signal<string | null>(null);
  protected readonly selectedReservation = signal<ReservationResponse | null>(null);
  protected readonly reservationResults = signal<readonly ReservationResponse[]>([]);
  protected readonly reservationSearching = signal(false);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  protected readonly invoiceId = computed(() => this.params().get('id'));
  protected readonly isEdit = computed(() => this.invoiceId() !== null);
  /** Duzenleme modunda taslak degilse duzenleme yolu kapalidir. */
  protected readonly editLocked = computed(() => {
    const status = this.editStatus();
    return status !== null && !isEditableInvoice(status);
  });

  protected readonly form = this.formBuilder.nonNullable.group({
    culture: [''],
    lineItems: this.formBuilder.array<FormGroup>([]),
  });

  protected get lineItems(): FormArray<FormGroup> {
    return this.form.controls.lineItems as FormArray<FormGroup>;
  }

  /**
   * Satir brut toplami — **yalnizca onizleme**. Fatura toplamlari sunucuda
   * satir bazinda yuvarlanarak hesaplanir; bu deger kaydedilmez ve gonderilmez.
   */
  protected readonly previewGross = computed(() => {
    void this.lineChanges();
    return this.lineItems.controls.reduce((total, group) => {
      const quantity = parseDecimal(group.get('quantity')?.value) ?? 0;
      const unitPrice = parseDecimal(group.get('unitPrice')?.value) ?? 0;
      return total + quantity * unitPrice;
    }, 0);
  });

  private readonly lineChanges = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  constructor() {
    void this.guests.load();

    const id = this.invoiceId();
    if (id) {
      void this.loadInvoice(id);
      return;
    }

    // Rezervasyon detayindan gelen on-doldurma: yol otomatik secilir.
    const reservationId = this.queryParams().get('reservationId');
    if (reservationId) {
      this.source.set('reservation');
      void this.loadReservation(reservationId);
    } else {
      this.addLine();
    }
  }

  protected setSource(source: InvoiceSource): void {
    this.source.set(source);
    if (source === 'manual' && this.lineItems.length === 0) {
      this.addLine();
    }
  }

  protected addLine(): void {
    if (this.lineItems.length >= INVOICE_LIMITS.lineItemsMax) {
      return;
    }
    this.lineItems.push(
      this.formBuilder.nonNullable.group({
        type: ['Extra', [Validators.required]],
        description: [
          '',
          [Validators.required, Validators.maxLength(INVOICE_LIMITS.descriptionMaxLength)],
        ],
        quantity: [1, [Validators.required]],
        unitPrice: ['', [Validators.required]],
        serviceDate: [''],
      }),
    );
  }

  protected removeLine(index: number): void {
    if (this.lineItems.length > INVOICE_LIMITS.lineItemsMin) {
      this.lineItems.removeAt(index);
    }
  }

  protected selectGuest(guestId: string): void {
    this.selectedGuestId.set(guestId || null);
  }

  protected searchGuests(term: string): void {
    void this.guests.load(term);
  }

  /** Rezervasyon arama (`GET /reservations?search=`). */
  protected async searchReservations(term: string): Promise<void> {
    this.reservationSearching.set(true);
    try {
      const result = await firstValueFrom(
        this.reservationsApi.list({ page: 1, pageSize: 20, search: term.trim() || null }),
      );
      this.reservationResults.set(result.items);
    } catch {
      this.reservationResults.set([]);
    } finally {
      this.reservationSearching.set(false);
    }
  }

  protected selectReservation(reservation: ReservationResponse): void {
    this.selectedReservation.set(reservation);
  }

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.formErrorKey.set(null);
    this.formErrors.set([]);
    this.serverDetail.set(null);

    if (this.editLocked()) {
      return;
    }

    const culture = this.form.controls.culture.value;
    const cultureValue = isAppLanguage(culture) ? culture : null;

    this.saving.set(true);
    try {
      const id = this.invoiceId();
      if (id) {
        const request: UpdateInvoiceRequest = {
          culture: cultureValue,
          lineItems: this.collectLines(),
        };
        if (request.lineItems.length === 0) {
          this.formErrorKey.set('invoices.form.validation.lineRequired');
          return;
        }
        await firstValueFrom(this.api.update(id, request));
        await this.router.navigate(['/invoices', id]);
        return;
      }

      const request = this.buildCreateRequest(cultureValue);
      if (request === null) {
        return;
      }
      const created = await firstValueFrom(this.api.create(request));
      await this.router.navigate(['/invoices', created.id]);
    } catch (error: unknown) {
      this.handleWriteError(toApiError(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    const id = this.invoiceId();
    void this.router.navigate(id ? ['/invoices', id] : ['/invoices']);
  }

  protected lineErrorKey(index: number, controlName: string): string | null {
    const control = this.lineItems.at(index)?.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    if (control.errors?.['required']) {
      return `invoices.form.validation.${controlName}Required`;
    }
    if (control.errors?.['maxlength']) {
      return 'invoices.form.validation.descriptionLength';
    }
    return null;
  }

  /**
   * `POST /invoices` govdesi — iki yol **birbirini disladigi** icin tek bir
   * govde uretilir; rezervasyon yolunda `lineItems` **hic** eklenmez.
   */
  private buildCreateRequest(culture: string | null): CreateInvoiceRequest | null {
    if (this.source() === 'reservation') {
      const reservation = this.selectedReservation();
      if (!reservation) {
        this.formErrorKey.set('invoices.form.validation.reservationRequired');
        return null;
      }
      return {
        reservationId: reservation.id,
        culture: isAppLanguage(culture) ? culture : null,
      };
    }

    const guestId = this.selectedGuestId();
    if (!guestId) {
      this.formErrorKey.set('invoices.form.validation.guestRequired');
      return null;
    }

    const lineItems = this.collectLines();
    if (lineItems.length === 0) {
      this.formErrorKey.set('invoices.form.validation.lineRequired');
      this.form.markAllAsTouched();
      return null;
    }

    return {
      guestId,
      culture: isAppLanguage(culture) ? culture : null,
      lineItems,
    };
  }

  /**
   * Satirlari yazma sozlesmesine cevirir.
   *
   * Sayisal alanlar: `<input type="number">` + `formControlName` ikilisinde
   * Angular kontrole **sayi** yazar; `parseDecimal` hem sayi hem metin (de-DE
   * virgullu) girdisini cozer — `.trim()` cagrilmaz.
   */
  private collectLines(): readonly InvoiceLineItemRequest[] {
    const lines: InvoiceLineItemRequest[] = [];
    for (const group of this.lineItems.controls) {
      const type = group.get('type')?.value;
      const description = String(group.get('description')?.value ?? '').trim();
      const quantity = parseDecimal(group.get('quantity')?.value);
      const unitPrice = parseDecimal(group.get('unitPrice')?.value);
      const serviceDate = String(group.get('serviceDate')?.value ?? '');

      if (!description || quantity === null || unitPrice === null) {
        continue;
      }
      lines.push({
        type: isInvoiceLineType(type) ? type : 'Extra',
        description,
        quantity,
        unitPrice,
        serviceDate: serviceDate || null,
      });
    }
    return lines;
  }

  private async loadInvoice(id: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const invoice = await firstValueFrom(this.api.getById(id));
      this.editStatus.set(invoice.status);
      this.form.patchValue({ culture: invoice.culture });

      this.lineItems.clear();
      for (const line of invoice.lineItems) {
        this.lineItems.push(
          this.formBuilder.nonNullable.group({
            type: [line.type, [Validators.required]],
            description: [
              line.description,
              [Validators.required, Validators.maxLength(INVOICE_LIMITS.descriptionMaxLength)],
            ],
            quantity: [line.quantity, [Validators.required]],
            unitPrice: [String(line.unitPrice), [Validators.required]],
            serviceDate: [line.serviceDate ?? ''],
          }),
        );
      }
      if (this.lineItems.length === 0) {
        this.addLine();
      }
    } catch (error: unknown) {
      this.loadError.set(toApiError(error));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadReservation(id: string): Promise<void> {
    this.loading.set(true);
    try {
      this.selectedReservation.set(await firstValueFrom(this.reservationsApi.getById(id)));
    } catch (error: unknown) {
      this.loadError.set(toApiError(error));
    } finally {
      this.loading.set(false);
    }
  }

  private handleWriteError(error: ApiError): void {
    this.serverDetail.set(error.detail ?? null);

    if (error.status === 409) {
      // Ornek: ayni rezervasyon icin iptal edilmemis bir fatura zaten var.
      this.formErrorKey.set('invoices.form.conflict');
      return;
    }

    const unmatched = applyApiFieldErrors(this.form, error);
    this.formErrors.set(unmatched);
    if (!error.fieldErrors || Object.keys(error.fieldErrors).length === 0) {
      this.formErrorKey.set(error.messageKey);
    }
  }
}
