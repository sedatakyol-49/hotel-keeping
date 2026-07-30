import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { InvoicesApi } from '../../core/api/invoices.api';
import { toApiError } from '../../core/interceptors/problem-details.mapper';
import {
  canCancelInvoice,
  canFinalizeInvoice,
  canRecordPayment,
  isEditableInvoice,
  producesCancellationInvoice,
  type InvoiceDetailResponse,
  type RecordPaymentRequest,
} from '../../core/models/invoice.model';
import type { ApiError } from '../../core/models/problem-details.model';

/** Detay ekraninda yurutulen yazma islemi. */
export type InvoiceAction = 'finalize' | 'cancel' | 'payment';

/**
 * Fatura detayi signal store'u (`GET /invoices/{id}`).
 *
 * Aksiyonlarin **gorunurlugu** durum makinesinden turetilir:
 * - `isEditable`: yalnizca `Draft` (GoBD §6.1) — `Finalized` faturada duzenleme
 *   aksiyonu ekranda **hic olusmaz**; sunucu 409 dondururdu ama kullaniciya
 *   yasak yolu gostermeyiz.
 * - `canFinalize`: yalnizca `Draft`.
 * - `canCancel`: `Cancelled` disindaki her durum.
 * - `canPay`: yalnizca `Finalized` **ve** brut tutar > 0 (storno'ya odeme yok).
 *
 * `producesStorno` iptal onay metnini dallandirir: taslakta storno **uretilmez**,
 * kesinlesmis/odenmis faturada orijinal korunur ve yeni bir Stornorechnung kesilir.
 */
@Injectable({ providedIn: 'root' })
export class InvoiceDetailStore {
  private readonly api = inject(InvoicesApi);

  private readonly _invoice = signal<InvoiceDetailResponse | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<ApiError | null>(null);
  private readonly _pendingAction = signal<InvoiceAction | null>(null);
  private readonly _actionError = signal<ApiError | null>(null);
  /** Iptal sonucu olusan Stornorechnung (kullaniciya baglanti verilir). */
  private readonly _cancellationInvoiceId = signal<string | null>(null);
  private readonly _lastAction = signal<InvoiceAction | null>(null);

  private requestToken = 0;

  readonly invoice = this._invoice.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly pendingAction = this._pendingAction.asReadonly();
  readonly actionError = this._actionError.asReadonly();
  readonly cancellationInvoiceId = this._cancellationInvoiceId.asReadonly();
  readonly lastAction = this._lastAction.asReadonly();

  readonly status = computed(() => this._invoice()?.status ?? null);

  /** Yalnizca `Draft` duzenlenebilir. */
  readonly isEditable = computed(() => {
    const status = this.status();
    return status !== null && isEditableInvoice(status);
  });
  readonly canFinalize = computed(() => {
    const status = this.status();
    return status !== null && canFinalizeInvoice(status);
  });
  readonly canCancel = computed(() => {
    const status = this.status();
    return status !== null && canCancelInvoice(status);
  });
  readonly canPay = computed(() => {
    const invoice = this._invoice();
    return invoice !== null && canRecordPayment(invoice);
  });
  /** Iptal bir Stornorechnung uretecek mi (onay metni buna gore dallanir). */
  readonly producesStorno = computed(() => {
    const status = this.status();
    return status !== null && producesCancellationInvoice(status);
  });

  readonly hasPayments = computed(() => (this._invoice()?.payments.length ?? 0) > 0);
  readonly outstanding = computed(() => this._invoice()?.outstandingAmount ?? 0);
  /** Kurtaxe ayri gosterilir; KDV matrahina dahil degildir. */
  readonly hasCityTax = computed(() => (this._invoice()?.cityTaxAmount ?? 0) !== 0);

  async load(id: string): Promise<void> {
    const token = ++this.requestToken;
    this._loading.set(true);
    this._error.set(null);
    this._actionError.set(null);
    this._cancellationInvoiceId.set(null);
    this._lastAction.set(null);

    try {
      const invoice = await firstValueFrom(this.api.getById(id));
      if (token !== this.requestToken) {
        return;
      }
      this._invoice.set(invoice);
    } catch (error: unknown) {
      if (token !== this.requestToken) {
        return;
      }
      this._invoice.set(null);
      this._error.set(toApiError(error));
    } finally {
      if (token === this.requestToken) {
        this._loading.set(false);
      }
    }
  }

  async reload(): Promise<void> {
    const id = this._invoice()?.id;
    if (id) {
      await this.load(id);
    }
  }

  /** `POST /invoices/{id}/finalize` — **geri alinamaz**; numara atanir. */
  async finalize(): Promise<ApiError | null> {
    return this.run('finalize', (id) => firstValueFrom(this.api.finalize(id)));
  }

  /**
   * `POST /invoices/{id}/cancel`.
   *
   * Kesinlesmis/odenmis faturada sunucu **yeni bir Stornorechnung** dondurur;
   * yanit iptal faturasinin kendisi olabilecegi icin `cancelsInvoiceId`
   * kontrol edilir ve kullaniciya storno'ya baglanti sunulur.
   */
  async cancel(reason: string | null): Promise<ApiError | null> {
    const wasStorno = this.producesStorno();
    const originalId = this._invoice()?.id ?? null;

    const error = await this.run('cancel', (id) =>
      firstValueFrom(this.api.cancel(id, reason ? { reason } : {})),
    );
    if (error !== null) {
      return error;
    }

    const result = this._invoice();
    if (wasStorno && result) {
      // Yanit ya orijinalin guncel hali (cancelledByInvoiceId dolu) ya da
      // dogrudan storno faturasidir (cancelsInvoiceId dolu).
      const stornoId =
        result.cancelledByInvoiceId ?? (result.cancelsInvoiceId ? result.id : null);
      this._cancellationInvoiceId.set(stornoId);

      // Yanit storno faturasi ise orijinali yeniden yukleyip ekranda tutariz.
      if (result.cancelsInvoiceId && originalId && result.id !== originalId) {
        await this.load(originalId);
        this._cancellationInvoiceId.set(stornoId);
        this._lastAction.set('cancel');
      }
    }
    return null;
  }

  /**
   * `POST /invoices/{id}/payments` — **fazla odeme 409** doner (kurus
   * toleransi yoktur); hata cagirana dondurulur ki ekran bunu `amount` alanina
   * baglayabilsin.
   */
  async recordPayment(request: RecordPaymentRequest): Promise<ApiError | null> {
    return this.run('payment', (id) => firstValueFrom(this.api.recordPayment(id, request)));
  }

  clearActionError(): void {
    this._actionError.set(null);
  }

  clearLastAction(): void {
    this._lastAction.set(null);
    this._cancellationInvoiceId.set(null);
  }

  private async run(
    action: InvoiceAction,
    call: (id: string) => Promise<InvoiceDetailResponse>,
  ): Promise<ApiError | null> {
    const current = this._invoice();
    if (!current) {
      return null;
    }

    this._pendingAction.set(action);
    this._actionError.set(null);

    try {
      this._invoice.set(await call(current.id));
      this._lastAction.set(action);
      return null;
    } catch (error: unknown) {
      const apiError = toApiError(error);
      this._actionError.set(apiError);
      return apiError;
    } finally {
      this._pendingAction.set(null);
    }
  }
}
