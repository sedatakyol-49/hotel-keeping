import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { LanguageStore, formatIsoDate, formatMoney } from '@hotelcore/shared';

import type { PublicGuestField } from '../../core/api/public-models';
import { languagePath } from '../../core/i18n/language-url';
import { BookingStore } from '../../core/state/booking.store';
import { HoldStore } from '../../core/state/hold.store';
import { ErrorPanel } from '../../shared/ui/error-panel/error-panel';
import { CheckField } from '../../shared/ui/form/check-field';
import { ErrorSummary } from '../../shared/ui/form/error-summary';
import { TextField } from '../../shared/ui/form/text-field';
import { HoldTimer } from '../../shared/ui/hold-timer/hold-timer';
import { Notice } from '../../shared/ui/notice/notice';
import { OrderSummary } from '../../shared/ui/order-summary/order-summary';
import { PriceBlock } from '../../shared/ui/price-block/price-block';
import { WithdrawalNotice } from '../../shared/ui/withdrawal-notice/withdrawal-notice';
import {
  bookingFormState,
  problemFor,
  toCreateBookingRequest,
  validateBookingForm,
} from './booking-form';

/**
 * ===========================================================================
 * REZERVASYON ADIMI — hukukun en yogun oldugu ekran
 * ===========================================================================
 *
 * RENDER MODU: **yalnizca istemci** (app.routes.server.ts). Bu sayfa misafirin
 * adini, e-postasini ve onaylarini tasir; sunucuda render etmenin SEO faydasi
 * sifirdir (`noindex`), buna karsilik kisisel veriyi sunucu bellegine ve olasi
 * ara onbelleklere tasir.
 *
 * SAYFANIN SIRASI RASTGELE DEGIL:
 *   1. Geri sayim + konaklama ozeti  — ne kadar sure var, ne rezerve ediliyor,
 *   2. Misafir bilgileri             — yalnizca sunucunun istedigi alanlar,
 *   3. Odeme bilgisi                 — girişte odeme; **kart alani yok**,
 *   4. Cayma hakki bildirimi (§312g) — ayri kutu, onay kutusu hemen altinda,
 *   5. Onaylar (AGB, aydinlatma, 18+, pazarlama[ops.]),
 *   6. §312j Abs. 2 ZORUNLU OZET + siparis dugmesi — arada hicbir sey yok.
 *
 * Adim 6 tek bir bileşendir (`hcg-order-summary`); ozet ile dugme arasina bir
 * sey sokmak yapisal olarak mumkun degildir.
 *
 * HATA/KURTARMA:
 *   - HOLD_EXPIRED / sayac sifirlandi -> "yeni teklif al" (ayni parametreler);
 *     yeni fiyat oncekinden farkliysa **acikca** gosterilir.
 *   - SUMMARY_CHANGED / LEGAL_TEXT_CHANGED -> akis DURUR; ozet tazelenir ve
 *     kullanicidan **yeniden onay** istenir. Sessizce yeni hash ile tekrar
 *     gondermek §312j'yi delerdi.
 *   - ROOM_NO_LONGER_AVAILABLE -> arama sonuclarina donus.
 */
@Component({
  selector: 'hcg-booking-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    TextField,
    CheckField,
    ErrorSummary,
    ErrorPanel,
    HoldTimer,
    Notice,
    OrderSummary,
    PriceBlock,
    WithdrawalNotice,
  ],
  template: `
    <div class="hcg-shell py-10">
      <p class="eyebrow">{{ 'booking.eyebrow' | translate }}</p>
      <h1 class="mt-3 text-headline">{{ 'booking.title' | translate }}</h1>

      @if (hold.loading() && hold.hold() === null) {
        <p class="mt-10 label-mono text-ink-muted" role="status" data-testid="booking-loading">
          {{ 'common.loading' | translate }}
        </p>
      } @else if (hold.hold(); as current) {
        <div class="mt-8 grid gap-10 lg:grid-cols-[1fr_20rem] lg:gap-14">
          <!-- ================= SOL: form ve onaylar ================= -->
          <div>
            @if (expired()) {
              <!-- KURTARMA: sure doldu -->
              <div class="mb-8" data-testid="hold-expired">
                <hcg-notice
                  tone="warning"
                  [assertive]="true"
                  [label]="'hold.expiredLabel' | translate"
                  [heading]="'hold.expiredTitle' | translate"
                >
                  <p>{{ 'hold.expiredBody' | translate }}</p>
                  <div data-notice-actions class="mt-4 flex flex-wrap gap-3">
                    <button
                      type="button"
                      class="hcg-action"
                      [disabled]="hold.loading()"
                      data-testid="hold-renew"
                      (click)="renew()"
                    >
                      {{ 'hold.renew' | translate }}
                    </button>
                    <a [routerLink]="searchPath()" class="hcg-action hcg-action--quiet">
                      {{ 'hold.backToSearch' | translate }}
                    </a>
                  </div>
                </hcg-notice>
              </div>
            }

            @if (priceChanged(); as change) {
              <div class="mb-8" data-testid="price-changed">
                <hcg-notice
                  tone="warning"
                  [assertive]="true"
                  [heading]="'hold.priceChangedTitle' | translate"
                >
                  {{
                    'hold.priceChangedBody'
                      | translate: { previous: change.previous, current: change.current }
                  }}
                </hcg-notice>
              </div>
            }

            @if (booking.requiresReconfirmation()) {
              <div class="mb-8" data-testid="summary-changed">
                <hcg-notice
                  tone="danger"
                  [assertive]="true"
                  [label]="'order.reconfirmLabel' | translate"
                  [heading]="'order.reconfirmTitle' | translate"
                >
                  <p>{{ 'order.reconfirmBody' | translate }}</p>
                  <div data-notice-actions class="mt-4">
                    <button
                      type="button"
                      class="hcg-action"
                      data-testid="summary-reconfirm"
                      (click)="acceptNewSummary()"
                    >
                      {{ 'order.reconfirmAction' | translate }}
                    </button>
                  </div>
                </hcg-notice>
              </div>
            } @else if (booking.error(); as error) {
              <div class="mb-8">
                <hcg-error-panel [error]="error" (recover)="recover()" />
              </div>
            }

            <form novalidate (submit)="$event.preventDefault()" data-testid="booking-form">
              <hcg-error-summary [problems]="form.problems()" />

              <!-- ---------- Misafir bilgileri ---------- -->
              <section class="mt-8" aria-labelledby="guest-heading">
                <h2 id="guest-heading" class="border-b border-rule pb-2 label-mono text-ink-muted">
                  {{ 'booking.guest.title' | translate }}
                </h2>

                <div class="mt-5 grid gap-5 sm:grid-cols-2">
                  @if (shows('firstName')) {
                    <hcg-text-field
                      name="firstName"
                      [label]="'booking.guest.firstName' | translate"
                      [value]="form.value().firstName"
                      [required]="true"
                      [requiredText]="'form.required' | translate"
                      autocomplete="given-name"
                      [maxLength]="100"
                      [error]="error('firstName')"
                      (valueChange)="form.patch({ firstName: $event })"
                    />
                  }
                  @if (shows('lastName')) {
                    <hcg-text-field
                      name="lastName"
                      [label]="'booking.guest.lastName' | translate"
                      [value]="form.value().lastName"
                      [required]="true"
                      [requiredText]="'form.required' | translate"
                      autocomplete="family-name"
                      [maxLength]="100"
                      [error]="error('lastName')"
                      (valueChange)="form.patch({ lastName: $event })"
                    />
                  }
                  @if (shows('email')) {
                    <hcg-text-field
                      name="email"
                      type="email"
                      inputMode="email"
                      [label]="'booking.guest.email' | translate"
                      [hint]="'booking.guest.emailHint' | translate"
                      [value]="form.value().email"
                      [required]="true"
                      [requiredText]="'form.required' | translate"
                      autocomplete="email"
                      [maxLength]="256"
                      [error]="error('email')"
                      (valueChange)="form.patch({ email: $event })"
                    />
                  }
                  @if (shows('phone')) {
                    <hcg-text-field
                      name="phone"
                      type="tel"
                      inputMode="tel"
                      [label]="'booking.guest.phone' | translate"
                      [hint]="'booking.guest.optional' | translate"
                      [value]="form.value().phone"
                      autocomplete="tel"
                      [maxLength]="32"
                      [error]="error('phone')"
                      (valueChange)="form.patch({ phone: $event })"
                    />
                  }
                </div>

                <p class="mt-4 text-xs text-ink-faint" data-testid="minimization-note">
                  {{ 'booking.guest.minimizationNote' | translate }}
                </p>
              </section>

              <!-- ---------- Konaklama ayrintilari (opsiyonel) ---------- -->
              @if (shows('estimatedArrivalLocalTime') || shows('guestNote')) {
                <section class="mt-10" aria-labelledby="stay-heading">
                  <h2 id="stay-heading" class="border-b border-rule pb-2 label-mono text-ink-muted">
                    {{ 'booking.stay.title' | translate }}
                  </h2>

                  <div class="mt-5 grid gap-5">
                    @if (shows('estimatedArrivalLocalTime')) {
                      <div class="sm:max-w-48">
                        <hcg-text-field
                          name="estimatedArrivalLocalTime"
                          type="time"
                          inputMode="numeric"
                          [label]="'booking.stay.arrival' | translate"
                          [hint]="'booking.stay.arrivalHint' | translate"
                          [value]="form.value().estimatedArrivalLocalTime"
                          [error]="error('estimatedArrivalLocalTime')"
                          (valueChange)="form.patch({ estimatedArrivalLocalTime: $event })"
                        />
                      </div>
                    }
                    @if (shows('guestNote')) {
                      <hcg-text-field
                        name="guestNote"
                        [multiline]="true"
                        [rows]="4"
                        [label]="'booking.stay.note' | translate"
                        [hint]="'booking.stay.noteHint' | translate"
                        [value]="form.value().guestNote"
                        [maxLength]="500"
                        [error]="error('guestNote')"
                        (valueChange)="form.patch({ guestNote: $event })"
                      />
                    }
                  </div>
                </section>
              }

              <!-- ---------- Fatura adresi (opsiyonel blok) ---------- -->
              @if (shows('invoiceAddress')) {
                <section class="mt-10" aria-labelledby="invoice-heading">
                  <h2
                    id="invoice-heading"
                    class="border-b border-rule pb-2 label-mono text-ink-muted"
                  >
                    {{ 'booking.invoice.title' | translate }}
                  </h2>

                  <div class="mt-3">
                    <hcg-check-field
                      name="invoiceRequested"
                      [checked]="form.value().invoiceRequested"
                      (checkedChange)="form.patch({ invoiceRequested: $event })"
                    >
                      {{ 'booking.invoice.toggle' | translate }}
                    </hcg-check-field>
                  </div>

                  @if (form.value().invoiceRequested) {
                    <div class="mt-4 grid gap-5 sm:grid-cols-2" data-testid="invoice-block">
                      <hcg-text-field
                        name="invoiceCompany"
                        [label]="'booking.invoice.company' | translate"
                        [value]="form.value().invoiceCompany"
                        autocomplete="organization"
                        [maxLength]="200"
                        [error]="error('invoiceCompany')"
                        (valueChange)="form.patch({ invoiceCompany: $event })"
                      />
                      <hcg-text-field
                        name="invoiceVatId"
                        [label]="'booking.invoice.vatId' | translate"
                        [value]="form.value().invoiceVatId"
                        [maxLength]="32"
                        [error]="error('invoiceVatId')"
                        (valueChange)="form.patch({ invoiceVatId: $event })"
                      />
                      <hcg-text-field
                        name="invoiceAddressLine"
                        [label]="'booking.invoice.addressLine' | translate"
                        [value]="form.value().invoiceAddressLine"
                        [required]="true"
                        [requiredText]="'form.required' | translate"
                        autocomplete="street-address"
                        [maxLength]="256"
                        [error]="error('invoiceAddressLine')"
                        (valueChange)="form.patch({ invoiceAddressLine: $event })"
                      />
                      <div class="grid grid-cols-[8rem_1fr] gap-4">
                        <hcg-text-field
                          name="invoicePostalCode"
                          inputMode="numeric"
                          [label]="'booking.invoice.postalCode' | translate"
                          [value]="form.value().invoicePostalCode"
                          autocomplete="postal-code"
                          [maxLength]="16"
                          [error]="error('invoicePostalCode')"
                          (valueChange)="form.patch({ invoicePostalCode: $event })"
                        />
                        <hcg-text-field
                          name="invoiceCity"
                          [label]="'booking.invoice.city' | translate"
                          [value]="form.value().invoiceCity"
                          autocomplete="address-level2"
                          [maxLength]="100"
                          [error]="error('invoiceCity')"
                          (valueChange)="form.patch({ invoiceCity: $event })"
                        />
                      </div>
                      <hcg-text-field
                        name="invoiceCountry"
                        [label]="'booking.invoice.country' | translate"
                        [hint]="'booking.invoice.countryHint' | translate"
                        [value]="form.value().invoiceCountry"
                        autocomplete="country"
                        [maxLength]="2"
                        [error]="error('invoiceCountry')"
                        (valueChange)="form.patch({ invoiceCountry: $event })"
                      />
                    </div>
                  }
                </section>
              }

              <!-- ---------- Odeme ---------- -->
              <section class="mt-10" aria-labelledby="payment-heading">
                <h2 id="payment-heading" class="border-b border-rule pb-2 label-mono text-ink-muted">
                  {{ 'booking.payment.title' | translate }}
                </h2>
                <p class="mt-4 text-sm" data-testid="payment-method">
                  {{ 'booking.payment.' + paymentMethod() | translate }}
                </p>
                <p class="mt-2 text-xs text-ink-faint" data-testid="payment-no-card">
                  {{ 'booking.payment.noCardNote' | translate }}
                </p>
              </section>

              <!-- ---------- §312g Abs. 2 Nr. 9 ---------- -->
              <section class="mt-10" aria-labelledby="withdrawal-heading">
                <h2
                  id="withdrawal-heading"
                  class="border-b border-rule pb-2 label-mono text-ink-muted"
                >
                  {{ 'legal.withdrawal.sectionTitle' | translate }}
                </h2>
                <div class="mt-4">
                  <hcg-withdrawal-notice [right]="current.legal.withdrawalRight" />
                </div>
                <div class="mt-3">
                  <hcg-check-field
                    name="withdrawalAcknowledged"
                    [checked]="form.value().withdrawalAcknowledged"
                    [error]="error('withdrawalAcknowledged')"
                    (checkedChange)="form.patch({ withdrawalAcknowledged: $event })"
                  >
                    {{ 'booking.consents.withdrawal' | translate }}
                  </hcg-check-field>
                </div>
              </section>

              <!-- ---------- Onaylar ---------- -->
              <section class="mt-10" aria-labelledby="consents-heading">
                <h2
                  id="consents-heading"
                  class="border-b border-rule pb-2 label-mono text-ink-muted"
                >
                  {{ 'booking.consents.title' | translate }}
                </h2>

                <div class="mt-3 grid gap-1">
                  <hcg-check-field
                    name="termsAccepted"
                    [checked]="form.value().termsAccepted"
                    [error]="error('termsAccepted')"
                    (checkedChange)="form.patch({ termsAccepted: $event })"
                  >
                    {{ 'booking.consents.terms' | translate }}
                    <a [routerLink]="legalPath('terms')" target="_blank" rel="noopener">
                      {{ 'legal.terms.label' | translate }}
                    </a>
                    <span class="numeric text-xs text-ink-faint">
                      ({{ current.legal.terms.version }})
                    </span>
                  </hcg-check-field>

                  <hcg-check-field
                    name="privacyAcknowledged"
                    [checked]="form.value().privacyAcknowledged"
                    [error]="error('privacyAcknowledged')"
                    (checkedChange)="form.patch({ privacyAcknowledged: $event })"
                  >
                    {{ 'booking.consents.privacy' | translate }}
                    <a [routerLink]="legalPath('privacy')" target="_blank" rel="noopener">
                      {{ 'legal.privacy.label' | translate }}
                    </a>
                    <span class="numeric text-xs text-ink-faint">
                      ({{ current.legal.privacyNotice.version }})
                    </span>
                  </hcg-check-field>

                  <hcg-check-field
                    name="bookerIsAdult"
                    [checked]="form.value().bookerIsAdult"
                    [error]="error('bookerIsAdult')"
                    (checkedChange)="form.patch({ bookerIsAdult: $event })"
                  >
                    {{ 'booking.consents.adult' | translate }}
                  </hcg-check-field>

                  <!-- Pazarlama: OPSIYONEL ve varsayilan olarak ISARETSIZ -->
                  <hcg-check-field
                    name="marketingOptIn"
                    [checked]="form.value().marketingOptIn"
                    (checkedChange)="form.patch({ marketingOptIn: $event })"
                  >
                    {{ 'booking.consents.marketing' | translate }}
                  </hcg-check-field>
                </div>
              </section>

              <!-- ---------- §312j Abs. 2 ozet + dugme ---------- -->
              <div class="mt-12">
                <hcg-order-summary
                  [summary]="current.orderSummary"
                  [orderButton]="current.legal.orderButton"
                  [disabled]="expired() || booking.requiresReconfirmation()"
                  [busy]="booking.submitting()"
                  (confirm)="place($event)"
                />

                <p class="mt-3 text-xs text-ink-faint" data-testid="contract-conclusion">
                  {{ 'order.contract.' + current.legal.contractConclusion | translate }}
                </p>

                <button
                  type="button"
                  class="mt-6 text-xs text-ink-muted underline underline-offset-4"
                  data-testid="abandon"
                  (click)="abandon()"
                >
                  {{ 'hold.abandon' | translate }}
                </button>
              </div>
            </form>
          </div>

          <!-- ================= SAG: sayac + ozet ================= -->
          <aside class="order-first lg:order-none lg:sticky lg:top-6 lg:self-start">
            <hcg-hold-timer [seconds]="hold.remainingSeconds()" [expired]="expired()" />

            <div class="mt-6 border border-rule bg-paper-raised px-5 py-5">
              <p class="eyebrow">{{ 'booking.summary.title' | translate }}</p>
              <p class="mt-2 font-serif text-xl" data-testid="aside-room">
                {{ current.orderSummary.essentialFeatures.roomTypeName }}
              </p>
              <p class="numeric mt-1 text-sm text-ink-muted" data-testid="aside-dates">
                {{ stayText() }}
              </p>

              <div class="mt-5 border-t border-rule pt-4">
                <hcg-price-block [price]="current.price" [nights]="current.nights" variant="full" />
              </div>
            </div>

            <div class="mt-6 border border-rule px-5 py-4">
              <p class="eyebrow">{{ 'booking.cancellation.label' | translate }}</p>
              <p class="mt-2 text-xs text-ink-muted" data-testid="aside-cancellation">
                {{
                  (current.cancellationPolicy.isFreeCancellationAvailable
                    ? 'search.offer.freeCancellation'
                    : 'search.offer.restrictedCancellation'
                  ) | translate
                }}
              </p>
            </div>
          </aside>
        </div>
      } @else if (hold.error(); as error) {
        <div class="mt-10 max-w-measure">
          <hcg-error-panel [error]="error" (recover)="recoverFromHoldError()">
            <a data-error-extra [routerLink]="searchPath()" class="hcg-action">
              {{ 'hold.backToSearch' | translate }}
            </a>
          </hcg-error-panel>
        </div>
      } @else {
        <div class="mt-10 max-w-measure" data-testid="booking-no-hold">
          <hcg-notice tone="warning" [heading]="'booking.noHold.title' | translate">
            <p>{{ 'booking.noHold.body' | translate }}</p>
            <div data-notice-actions class="mt-4">
              <a [routerLink]="searchPath()" class="hcg-action">
                {{ 'hold.backToSearch' | translate }}
              </a>
            </div>
          </hcg-notice>
        </div>
      }
    </div>
  `,
})
export class BookingPage {
  /** `?holdToken=` — adresten gelir; yoksa oturum deposundan kurtarilir. */
  readonly holdToken = input('');

  protected readonly hold = inject(HoldStore);
  protected readonly booking = inject(BookingStore);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageStore);
  private readonly translate = inject(TranslateService);

  /*
   * Dogrulama fonksiyonu forma **enjekte edilir**: form durumu, ilk gonderimden
   * sonra her degisiklikte kendini yeniden dogrular (bkz. bookingFormState).
   */
  protected readonly form = bookingFormState((value) =>
    validateBookingForm(value, this.fields(), (key, params) => this.text(key, params)),
  );
  private readonly opened = signal('');

  protected readonly expired = this.hold.expired;

  /** Sunucunun bildirdigi alanlar — istemci bunun disina cikamaz. */
  private readonly fields = computed<ReadonlySet<PublicGuestField>>(() => {
    const current = this.hold.hold();
    if (current === null) {
      return new Set<PublicGuestField>();
    }
    return new Set<PublicGuestField>([
      ...current.requiredGuestFields,
      ...current.optionalGuestFields,
    ]);
  });

  protected readonly paymentMethod = computed(
    () => this.hold.hold()?.paymentOptions[0]?.method ?? 'PayAtProperty',
  );

  /** Yenileme sonrasi fiyat degistiyse iki tutar yan yana gosterilir. */
  protected readonly priceChanged = computed(() => {
    const previous = this.hold.previousTotal();
    const current = this.hold.hold();
    if (previous === null || current === null || previous === current.price.totalGross) {
      return null;
    }
    const language = this.language.current();
    return {
      previous: formatMoney(previous, current.price.currency, language),
      current: formatMoney(current.price.totalGross, current.price.currency, language),
    };
  });

  protected readonly stayText = computed(() => {
    const current = this.hold.hold();
    if (current === null) {
      return '';
    }
    const language = this.language.current();
    /* Gece sayisi BIRIMIYLE yazilir; "· 3" tek basina bir sey ifade etmez. */
    return this.text('order.durationValue', {
      checkIn: formatIsoDate(current.checkIn, language),
      checkOut: formatIsoDate(current.checkOut, language),
      nights: current.nights,
    });
  });

  constructor() {
    effect(() => {
      const token = this.holdToken() || this.hold.storedToken();
      if (token !== null && token.length > 0 && this.opened() !== token) {
        this.opened.set(token);
        this.hold.open(token);
      }
    });
  }

  protected shows(field: PublicGuestField): boolean {
    return this.fields().has(field);
  }

  protected error(field: string): string | null {
    return problemFor(this.form.problems(), field);
  }

  protected searchPath(): string {
    return languagePath(this.language.current(), 'search');
  }

  protected legalPath(slug: string): string {
    return languagePath(this.language.current(), 'legal', slug);
  }

  /**
   * SIPARIS. `orderButtonLabel` dugmeden **oldugu gibi** gelir (kanit kaydi);
   * `summaryHash` hold'da donmus ozetin hash'idir.
   */
  protected place(orderButtonLabel: string): void {
    const current = this.hold.hold();
    if (current === null || this.expired()) {
      return;
    }

    if (!this.form.submit()) {
      return;
    }

    const request = toCreateBookingRequest(
      current,
      this.form.value(),
      this.language.current(),
      orderButtonLabel,
    );

    this.booking.submit(request, (result) => {
      const token = result.accessToken ?? '';
      this.hold.clear();
      void this.router.navigate([
        languagePath(this.language.current(), 'confirmation', token),
      ]);
    });
  }

  protected renew(): void {
    this.hold.renew();
  }

  /** Ozet degisti -> tazelenmis teklif gosterilir, kullanici yeniden onaylar. */
  protected acceptNewSummary(): void {
    this.booking.acknowledgeReconfirmation();
    this.hold.refresh();
  }

  protected recover(): void {
    const error = this.booking.error();
    switch (error?.recovery) {
      case 'renewHold':
        this.hold.renew();
        break;
      case 'backToSearch':
        void this.router.navigate([this.searchPath()]);
        break;
      case 'reconfirmSummary':
        this.acceptNewSummary();
        break;
      default:
        this.booking.reset();
    }
  }

  protected recoverFromHoldError(): void {
    const error = this.hold.error();
    if (error?.recovery === 'renewHold') {
      /* Hold okunamadi: parametreler bilinmiyor, arama sonuclarina donulur. */
      void this.router.navigate([this.searchPath()]);
      return;
    }
    void this.router.navigate([this.searchPath()]);
  }

  /** Akistan cikis: envanter hemen serbest birakilir. */
  protected abandon(): void {
    this.hold.release();
    void this.router.navigate([this.searchPath()]);
  }

  private text(key: string, params?: Record<string, unknown>): string {
    const value: unknown = this.translate.instant(key, params);
    return typeof value === 'string' ? value : key;
  }
}
