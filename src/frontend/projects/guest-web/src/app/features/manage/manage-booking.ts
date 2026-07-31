import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { LanguageStore, formatMoney } from '@hotelcore/shared';

import { languagePath } from '../../core/i18n/language-url';
import { ManageBookingStore } from '../../core/state/manage-booking.store';
import { BookingView } from '../../shared/ui/booking-view/booking-view';
import { ErrorPanel } from '../../shared/ui/error-panel/error-panel';
import { CheckField } from '../../shared/ui/form/check-field';
import { TextField } from '../../shared/ui/form/text-field';
import { Notice } from '../../shared/ui/notice/notice';

/**
 * ===========================================================================
 * REZERVASYON GORUNTULEME VE IPTAL
 * ===========================================================================
 *
 * IPTAL AKISI IKI ADIMLIDIR — ve bu bilincli bir surtunmedir:
 *   Adim 1: "Iptal etmek istiyorum" (panel acilir)
 *   Adim 2: dogacak **tutar gosterilir**, ucret varsa **acikca teyit edilir**
 *           (`acknowledgedFeeAmount`), sonra iptal gonderilir.
 *
 * Sozlesme §7.3 bunu zorunlu kilar: ucret dogacaksa sunucu tutar teyidi
 * olmadan iptali reddeder (409 `FEE_ACKNOWLEDGEMENT_REQUIRED`). Amac misafirin
 * ucreti gormeden iptal etmesini engellemektir; arayuz de bu amaci tasimali,
 * tutari kucuk puntoyla dipnota atmamalidir.
 *
 * KURTAXE: iptal ucretinin matrahina **girmez** (konaklama gerceklesmedigi icin
 * vergi dogmaz). Bu, iptal ekraninda acikca yazar — misafir toplam tutarin
 * yuzdesini bekleyip konaklama tutarinin yuzdesini gorurse guvenini yitirir.
 *
 * `InHouse` / `Completed` durumunda online iptal yoktur; ekran oteli aramayi
 * onerir ve telefon numarasini gosterir (rezervasyon yanitindan gelir).
 */
@Component({
  selector: 'hcg-manage-booking-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, BookingView, ErrorPanel, Notice, CheckField, TextField],
  template: `
    <div class="hcg-shell py-12">
      @if (store.booking(); as booking) {
        <p class="eyebrow">{{ 'manage.eyebrow' | translate }}</p>
        <h1 class="mt-3 text-headline">{{ 'manage.detail.title' | translate }}</h1>

        @if (booking.status === 'Cancelled') {
          <div class="mt-8 max-w-measure" data-testid="cancelled-notice">
            <hcg-notice tone="warning" [heading]="'manage.cancelled.title' | translate">
              <p>{{ 'manage.cancelled.body' | translate }}</p>
              @if (booking.cancellation.chargedFeeAmount !== null) {
                <p class="numeric mt-2" data-testid="cancelled-fee">
                  {{
                    'manage.cancelled.fee'
                      | translate: { amount: money(booking.cancellation.chargedFeeAmount ?? 0) }
                  }}
                </p>
              }
            </hcg-notice>
          </div>
        }

        <div class="mt-10">
          <hcg-booking-view [booking]="booking" />
        </div>

        @if (booking.status !== 'Cancelled') {
          <section class="mt-12 border-t border-rule pt-8" aria-labelledby="cancel-heading">
            <h2 id="cancel-heading" class="label-mono text-ink-muted">
              {{ 'manage.cancel.title' | translate }}
            </h2>

            @if (!store.canCancel()) {
              <div class="mt-4 max-w-measure" data-testid="cancel-unavailable">
                <hcg-notice tone="neutral" [heading]="'manage.cancel.offlineTitle' | translate">
                  <p>{{ 'manage.cancel.offlineBody' | translate }}</p>
                  <p class="numeric mt-2" data-testid="cancel-phone">{{ booking.hotel.phone }}</p>
                </hcg-notice>
              </div>
            } @else if (!panelOpen()) {
              <button
                type="button"
                class="hcg-action hcg-action--quiet mt-4"
                data-testid="cancel-open"
                (click)="panelOpen.set(true)"
              >
                {{ 'manage.cancel.open' | translate }}
              </button>
            } @else {
              <div class="mt-4 max-w-measure border border-rule bg-paper-raised px-5 py-5">
                @if (store.cancelError(); as error) {
                  <div class="mb-5">
                    <hcg-error-panel [error]="error" (recover)="reload()" />
                  </div>
                }

                <p class="text-sm" data-testid="cancel-fee-statement">
                  {{
                    (fee() > 0 ? 'manage.cancel.feeDue' : 'manage.cancel.free')
                      | translate: { amount: money(fee()) }
                  }}
                </p>

                @if (fee() > 0) {
                  <p class="mt-2 text-xs text-ink-muted" data-testid="cancel-city-tax-note">
                    {{ 'manage.cancel.cityTaxNote' | translate }}
                  </p>

                  <div class="mt-4">
                    <hcg-check-field
                      name="feeAcknowledged"
                      [checked]="feeAcknowledged()"
                      [error]="feeError()"
                      (checkedChange)="feeAcknowledged.set($event)"
                    >
                      {{ 'manage.cancel.acknowledge' | translate: { amount: money(fee()) } }}
                    </hcg-check-field>
                  </div>
                }

                <div class="mt-5">
                  <hcg-text-field
                    name="cancelReason"
                    [multiline]="true"
                    [rows]="3"
                    [label]="'manage.cancel.reason' | translate"
                    [hint]="'manage.cancel.reasonHint' | translate"
                    [value]="reason()"
                    [maxLength]="500"
                    (valueChange)="reason.set($event)"
                  />
                </div>

                <div class="mt-6 flex flex-wrap gap-3">
                  <button
                    type="button"
                    class="hcg-action"
                    [disabled]="store.cancelling()"
                    data-testid="cancel-confirm"
                    (click)="confirmCancel()"
                  >
                    {{ (store.cancelling() ? 'manage.cancel.sending' : 'manage.cancel.confirm') | translate }}
                  </button>
                  <button
                    type="button"
                    class="hcg-action hcg-action--quiet"
                    data-testid="cancel-abort"
                    (click)="panelOpen.set(false)"
                  >
                    {{ 'manage.cancel.keep' | translate }}
                  </button>
                </div>
              </div>
            }
          </section>
        }

        <div class="mt-10 border-t border-rule pt-8">
          <a [routerLink]="homePath()" class="hcg-action hcg-action--quiet">
            {{ 'confirmation.home' | translate }}
          </a>
        </div>
      } @else if (store.error(); as error) {
        <p class="eyebrow">{{ 'manage.eyebrow' | translate }}</p>
        <h1 class="mt-3 text-headline">{{ 'manage.detail.missingTitle' | translate }}</h1>
        <div class="mt-8 max-w-measure">
          <hcg-error-panel [error]="error" (recover)="reload()">
            <a data-error-extra [routerLink]="lookupPath()" class="hcg-action">
              {{ 'manage.lookup.cta' | translate }}
            </a>
          </hcg-error-panel>
        </div>
      } @else {
        <h1 class="text-headline">{{ 'manage.detail.title' | translate }}</h1>
        <p class="mt-6 label-mono text-ink-muted" role="status" data-testid="manage-loading">
          {{ 'common.loading' | translate }}
        </p>
      }
    </div>
  `,
})
export class ManageBookingPage {
  readonly token = input.required<string>();

  protected readonly store = inject(ManageBookingStore);
  private readonly language = inject(LanguageStore);
  private readonly translate = inject(TranslateService);

  protected readonly panelOpen = signal(false);
  protected readonly feeAcknowledged = signal(false);
  protected readonly reason = signal('');
  private readonly feeProblem = signal(false);
  private readonly loaded = signal('');

  protected readonly fee = this.store.cancellationFee;

  protected readonly feeError = computed(() => {
    if (!this.feeProblem()) {
      return null;
    }
    this.translate.currentLang();
    const value: unknown = this.translate.instant('form.errors.feeAcknowledgeRequired');
    return typeof value === 'string' ? value : 'form.errors.feeAcknowledgeRequired';
  });

  constructor() {
    effect(() => {
      const token = this.token();
      if (token.length > 0 && this.loaded() !== token) {
        this.loaded.set(token);
        this.store.load(token);
      }
    });
  }

  protected confirmCancel(): void {
    const fee = this.fee();
    if (fee > 0 && !this.feeAcknowledged()) {
      this.feeProblem.set(true);
      return;
    }
    this.feeProblem.set(false);
    /* Ucretsiz pencerede `acknowledgedFeeAmount` GONDERILMEZ (sozlesme §7.3). */
    /* Panel ACIK kalir: hata olursa kullanici onu bu panelin icinde gorur. */
    this.store.cancel(this.token(), this.reason().trim() || null, fee > 0 ? fee : null);
  }

  protected reload(): void {
    this.store.load(this.token());
  }

  protected money(amount: number): string {
    const currency = this.store.booking()?.price.currency ?? 'EUR';
    return formatMoney(amount, currency, this.language.current());
  }

  protected homePath(): string {
    return languagePath(this.language.current());
  }

  protected lookupPath(): string {
    return languagePath(this.language.current(), 'manage');
  }
}
