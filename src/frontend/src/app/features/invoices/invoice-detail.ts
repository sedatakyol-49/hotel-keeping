import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import {
  INVOICE_LIMITS,
  INVOICE_LINE_TYPE_LABEL_KEYS,
  PAYMENT_METHODS,
  PAYMENT_METHOD_LABEL_KEYS,
  auditActionLabelKey,
  isPaymentMethod,
  type PaymentMethod,
  type RecordPaymentRequest,
} from '../../core/models/invoice.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { setServerError } from '../../shared/forms/api-field-errors';
import { decimalRangeValidator, parseDecimal } from '../../shared/forms/numeric-validators';
import { LocalizedDatePipe } from '../../shared/pipes/localized-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import { InvoiceDetailStore } from './invoice-detail.store';
import { InvoiceStatusBadge } from './invoice-status';

/**
 * Fatura detayi: satirlar, KDV kirilimi + Kurtaxe, odemeler, denetim izi.
 *
 * ### Gorunmeyen yollar
 * - **Duzenleme** yalnizca `Draft`'ta gorunur. `Finalized` faturada duzenleme
 *   baglantisi **hic render edilmez** (GoBD §6.1: kesinlesmis fatura
 *   degistirilemez). Sunucu 409 dondururdu; yasak yolu gostermeyiz.
 * - **PDF** dugmesi devre disidir ve nedeni yazilidir: `GET /invoices/{id}/pdf`
 *   bu fazda **501** doner. **Sahte indirme yapilmaz** — istek hic gonderilmez.
 *
 * ### Iptal onayi dallanir
 * - `Draft`: dogrudan iptal, **storno uretilmez** (numarasi olmayan taslak belge
 *   degildir, sekansta bosluk dogmaz).
 * - `Finalized`/`Paid`: orijinal **korunur** ve yeni bir **Stornorechnung**
 *   kesilir; toplamlari orijinalin negatifidir (orijinal + storno = 0).
 *
 * ### Fazla odeme
 * Sunucu **409** doner (kurus toleransi yok). Hata genel bir serit yerine
 * `amount` alanina baglanir — kullanici duzeltmesi gereken yeri gorur.
 */
@Component({
  selector: 'hc-invoice-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    LocalizedDatePipe,
    MoneyPipe,
    PageHeader,
    TableShell,
    Spinner,
    Button,
    InvoiceStatusBadge,
    HasPermissionDirective,
  ],
  templateUrl: './invoice-detail.html',
})
export class InvoiceDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly store = inject(InvoiceDetailStore);

  protected readonly createPermission = PERMISSIONS.InvoicesCreate;
  protected readonly approvePermission = PERMISSIONS.InvoicesApprove;
  protected readonly cancelPermission = PERMISSIONS.InvoicesCancel;
  protected readonly lineTypeLabelKeys = INVOICE_LINE_TYPE_LABEL_KEYS;
  protected readonly paymentMethods = PAYMENT_METHODS;
  protected readonly paymentMethodLabelKeys = PAYMENT_METHOD_LABEL_KEYS;
  protected readonly limits = INVOICE_LIMITS;

  /** Acik onay/panel — ayni anda yalnizca biri. */
  protected readonly panel = signal<'finalize' | 'cancel' | 'payment' | null>(null);

  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });

  protected readonly invoiceId = computed(() => this.params().get('id') ?? '');

  protected readonly paymentForm = this.formBuilder.nonNullable.group({
    method: ['Card' as PaymentMethod, [Validators.required]],
    amount: [
      '',
      [
        Validators.required,
        decimalRangeValidator({ min: 0, max: INVOICE_LIMITS.amountMax, exclusiveMin: true }),
      ],
    ],
    paidAt: [''],
    reference: ['', [Validators.maxLength(INVOICE_LIMITS.referenceMaxLength)]],
  });

  /**
   * Iptal onay metni **duruma gore** degisir. Bu bir kolaylik degil, sozlesme
   * farkidir: taslakta belge yoktur, kesinlesmis faturada muhasebe kaydi olusur.
   */
  protected readonly cancelConfirmKey = computed(() =>
    this.store.producesStorno() ? 'invoices.cancel.confirmStorno' : 'invoices.cancel.confirmDraft',
  );

  protected readonly actionErrorKey = computed(() => {
    const error = this.store.actionError();
    if (!error) {
      return null;
    }
    if (error.status === 409) {
      return 'invoices.actions.conflict';
    }
    if (error.status === 403) {
      return 'errors.forbidden';
    }
    return error.messageKey;
  });

  /**
   * Odeme alani hatasi (fazla odeme 409 burada gorunur).
   *
   * Bilincli olarak **metot**tur, `computed()` degil: kaynak `AbstractControl`
   * durumudur ve bir signal degildir; `computed` ilk degerini onbellege alir ve
   * `setErrors` sonrasi bir daha calismazdi (hata hic gorunmezdi).
   */
  protected amountErrorKey(): string | null {
    const control = this.paymentForm.controls.amount;
    if (control.valid || !control.touched) {
      return null;
    }
    const errors = control.errors ?? {};
    if (typeof errors['conflict'] === 'string') {
      return errors['conflict'];
    }
    if (errors['required']) {
      return 'invoices.payment.validation.amountRequired';
    }
    if (errors['decimalFormat'] || errors['decimalRange']) {
      return 'invoices.payment.validation.amount';
    }
    return null;
  }

  constructor() {
    effect(() => {
      const id = this.invoiceId();
      this.panel.set(null);
      if (id) {
        void this.store.load(id);
      }
    });
  }

  protected retry(): void {
    void this.store.reload();
  }

  protected openPanel(panel: 'finalize' | 'cancel' | 'payment'): void {
    this.store.clearActionError();
    if (panel === 'payment') {
      // Kalan bakiye on-doldurulur: en sik durum tam tahsilattir.
      this.paymentForm.reset({
        method: 'Card',
        amount: this.store.outstanding() > 0 ? String(this.store.outstanding()) : '',
        paidAt: '',
        reference: '',
      });
    }
    this.panel.set(panel);
  }

  protected closePanel(): void {
    this.panel.set(null);
    this.store.clearActionError();
  }

  protected async finalize(): Promise<void> {
    const error = await this.store.finalize();
    if (error === null) {
      this.panel.set(null);
    }
  }

  protected async cancel(reason: string): Promise<void> {
    const error = await this.store.cancel(reason.trim() || null);
    if (error === null) {
      this.panel.set(null);
    }
  }

  /** Odeme kaydi; fazla odeme (409) `amount` alanina baglanir. */
  protected async recordPayment(): Promise<void> {
    if (this.paymentForm.invalid) {
      this.paymentForm.markAllAsTouched();
      return;
    }

    const raw = this.paymentForm.getRawValue();
    // `amount` metin kontroludur (de-DE virgullu yazim); `parseDecimal` her iki
    // yazimi da cozer ve sayi girdisinde `.trim()` cagrilmaz.
    const amount = parseDecimal(raw.amount);
    if (amount === null) {
      this.paymentForm.controls.amount.markAsTouched();
      return;
    }

    const request: RecordPaymentRequest = {
      method: isPaymentMethod(raw.method) ? raw.method : 'Card',
      amount,
      // `<input type="datetime-local">` degeri zaman dilimsizdir; sunucu
      // gelecek tarihi 400 ile reddeder, bos birakilirsa sunucu saati kullanilir.
      paidAt: raw.paidAt ? new Date(raw.paidAt).toISOString() : null,
      reference: raw.reference.trim() || null,
    };

    const error = await this.store.recordPayment(request);
    if (error === null) {
      this.panel.set(null);
      return;
    }
    if (error.status === 409) {
      setServerError(this.paymentForm.controls.amount, 'invoices.payment.overpayment');
    }
  }

  protected auditLabelKey(action: string): string | null {
    return auditActionLabelKey(action);
  }

  protected dismissLastAction(): void {
    this.store.clearLastAction();
  }
}
