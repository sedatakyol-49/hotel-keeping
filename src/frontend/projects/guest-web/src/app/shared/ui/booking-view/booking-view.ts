import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { LanguageStore, formatInstant, formatIsoDate, formatMoney } from '@hotelcore/shared';

import type { PublicBookingResponse } from '../../../core/api/public-models';
import { Notice } from '../notice/notice';
import { PriceBlock } from '../price-block/price-block';
import { WithdrawalNotice } from '../withdrawal-notice/withdrawal-notice';

/**
 * Rezervasyonun tam gorunumu — onay ekrani ve sorgulama ekrani **ayni**
 * bileşeni kullanir.
 *
 * NEDEN TEK BILESEN: iki ekranda iki farkli ozet olsaydi, biri gunun birinde
 * Kurtaxe satirini ya da ucretsiz iptal son tarihini kaybederdi. Rezervasyonun
 * icerigi tek bir yerde tanimlidir; ekranlar yalnizca **cercevesini** ve
 * eylemlerini degistirir.
 *
 * Rezervasyon numarasi mono/tabular ve buyuk gosterilir: misafir bunu telefonda
 * okur, resepsiyonda soyler. Crockford Base32 alfabesi `I/L/O/U` icermez, yani
 * karakter karismasi yoktur — tipografi bunu desteklemeli.
 */
@Component({
  selector: 'hcg-booking-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, Notice, PriceBlock, WithdrawalNotice],
  template: `
    <div class="grid gap-8 lg:grid-cols-[1fr_20rem] lg:gap-12" data-testid="booking-view">
      <div>
        <!-- Referans + durum -->
        <div class="border border-rule bg-paper-raised px-5 py-4">
          <p class="eyebrow">{{ 'booking.reference' | translate }}</p>
          <p class="numeric mt-1 text-3xl" data-testid="booking-reference">
            {{ booking().bookingReference }}
          </p>
          <p class="mt-2 text-sm" [attr.data-testid]="'booking-status-' + booking().status">
            {{ 'booking.status.' + booking().status | translate }}
          </p>
        </div>

        <!-- Konaklama -->
        <dl class="mt-8 border-t border-rule">
          <div class="flex items-baseline justify-between gap-4 border-b border-rule py-3">
            <dt class="text-sm text-ink-muted">{{ 'booking.stay.roomType' | translate }}</dt>
            <dd class="text-sm" data-testid="stay-room-type">{{ booking().stay.roomTypeName }}</dd>
          </div>
          <div class="flex items-baseline justify-between gap-4 border-b border-rule py-3">
            <dt class="text-sm text-ink-muted">{{ 'booking.stay.dates' | translate }}</dt>
            <dd class="numeric text-right text-sm" data-testid="stay-dates">
              {{ dateRange() }}
              <span class="mt-0.5 block text-xs text-ink-faint">
                {{
                  'order.checkTimes'
                    | translate
                      : {
                          from: booking().stay.checkInFromLocal,
                          until: booking().stay.checkOutUntilLocal,
                        }
                }}
              </span>
            </dd>
          </div>
          <div class="flex items-baseline justify-between gap-4 border-b border-rule py-3">
            <dt class="text-sm text-ink-muted">{{ 'booking.stay.occupancy' | translate }}</dt>
            <dd class="numeric text-sm" data-testid="stay-occupancy">{{ occupancy() }}</dd>
          </div>
          <div class="flex items-baseline justify-between gap-4 border-b border-rule py-3">
            <dt class="text-sm text-ink-muted">{{ 'booking.guest.title' | translate }}</dt>
            <dd class="text-right text-sm" data-testid="booking-guest">
              {{ booking().guest.firstName }} {{ booking().guest.lastName }}
              <span class="mt-0.5 block text-xs text-ink-faint">{{ booking().guest.email }}</span>
            </dd>
          </div>
          <div class="flex items-baseline justify-between gap-4 border-b border-rule py-3">
            <dt class="text-sm text-ink-muted">{{ 'booking.hotel' | translate }}</dt>
            <dd class="text-right text-sm" data-testid="booking-hotel">
              {{ booking().hotel.name }}
              <span class="mt-0.5 block text-xs text-ink-faint">
                {{ booking().hotel.addressLine }}, {{ booking().hotel.postalCode }}
                {{ booking().hotel.city }}
              </span>
              <span class="numeric mt-0.5 block text-xs text-ink-faint">
                {{ booking().hotel.phone }}
              </span>
            </dd>
          </div>
        </dl>

        <!-- Iptal politikasi: MUTLAK son tarih, yuzde degil tutar -->
        <div class="mt-8">
          <hcg-notice
            [tone]="cancellationTone()"
            [label]="'booking.cancellation.label' | translate"
            [heading]="cancellationHeading() | translate"
          >
            <p data-testid="cancellation-deadline">
              {{
                'booking.cancellation.until'
                  | translate: { deadline: freeCancellationUntil() }
              }}
            </p>
            <p class="mt-2" data-testid="cancellation-fee">
              {{
                'booking.cancellation.lateFee'
                  | translate
                    : {
                        percent: booking().cancellation.lateCancellationFeePercent,
                        amount: money(booking().cancellation.lateCancellationFeeAmount),
                      }
              }}
            </p>
            @if (booking().cancellation.cityTaxRefundedOnCancellation) {
              <p class="mt-2 text-xs" data-testid="cancellation-city-tax">
                {{ 'booking.cancellation.cityTaxNote' | translate }}
              </p>
            }
            @if (booking().cancellation.chargedFeeAmount !== null) {
              <p class="numeric mt-2" data-testid="cancellation-charged">
                {{
                  'booking.cancellation.charged'
                    | translate: { amount: money(booking().cancellation.chargedFeeAmount ?? 0) }
                }}
              </p>
            }
          </hcg-notice>
        </div>

        <!-- §312g Abs. 2 Nr. 9 — rezervasyon aninda DONDURULMUS bildirim -->
        <div class="mt-6">
          <hcg-withdrawal-notice [right]="booking().legal.withdrawalRight" />
        </div>
      </div>

      <aside>
        <div class="border border-rule bg-paper-raised px-5 py-5">
          <hcg-price-block [price]="booking().price" [nights]="booking().stay.nights" variant="full" />
        </div>

        <div class="mt-6 border border-rule px-5 py-4">
          <p class="eyebrow">{{ 'booking.payment.title' | translate }}</p>
          <p class="mt-2 text-sm" data-testid="booking-payment">
            {{ 'booking.payment.' + booking().payment.method | translate }}
          </p>
          <p class="numeric mt-1 text-sm" data-testid="booking-due">
            {{
              'booking.payment.due' | translate: { amount: money(booking().payment.amountDueAtProperty) }
            }}
          </p>
          <p class="mt-2 text-xs text-ink-faint">{{ 'booking.payment.noCardNote' | translate }}</p>
        </div>

        <div class="mt-6 border border-rule px-5 py-4">
          <p class="eyebrow">{{ 'booking.legal.title' | translate }}</p>
          <ul class="numeric mt-2 grid gap-1 text-xs text-ink-muted">
            <li data-testid="legal-terms-version">
              {{
                'booking.legal.terms' | translate: { version: booking().legal.terms.version }
              }}
            </li>
            <li data-testid="legal-privacy-version">
              {{
                'booking.legal.privacy'
                  | translate: { version: booking().legal.privacyNotice.version }
              }}
            </li>
            <li data-testid="legal-button-label">
              {{
                'booking.legal.orderButton'
                  | translate: { label: booking().legal.orderButton.labelDe }
              }}
            </li>
          </ul>
        </div>
      </aside>
    </div>
  `,
})
export class BookingView {
  readonly booking = input.required<PublicBookingResponse>();

  private readonly language = inject(LanguageStore);
  private readonly translate = inject(TranslateService);

  /** Gece sayisi BIRIMIYLE yazilir; "· 3" tek basina bir sey ifade etmez. */
  protected readonly dateRange = computed(() => {
    const stay = this.booking().stay;
    const language = this.language.current();
    this.translate.currentLang();
    const value: unknown = this.translate.instant('order.durationValue', {
      checkIn: formatIsoDate(stay.checkIn, language),
      checkOut: formatIsoDate(stay.checkOut, language),
      nights: stay.nights,
    });
    return typeof value === 'string' ? value : '';
  });

  protected readonly occupancy = computed(() => {
    const stay = this.booking().stay;
    return `${stay.adults} / ${stay.children}`;
  });

  protected readonly freeCancellationUntil = computed(() =>
    formatInstant(this.booking().cancellation.freeCancellationUntil, this.language.current()),
  );

  protected readonly cancellationTone = computed(() =>
    this.booking().cancellation.isFreeCancellationAvailable ? 'neutral' : 'warning',
  );

  protected readonly cancellationHeading = computed(() =>
    this.booking().cancellation.isFreeCancellationAvailable
      ? 'booking.cancellation.freeTitle'
      : 'booking.cancellation.paidTitle',
  );

  protected money(amount: number): string {
    return formatMoney(amount, this.booking().price.currency, this.language.current());
  }
}
