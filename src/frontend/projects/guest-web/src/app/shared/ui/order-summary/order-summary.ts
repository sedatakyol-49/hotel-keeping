import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { LanguageStore, formatIsoDate, formatMoney } from '@hotelcore/shared';

import type { PublicOrderButton, PublicOrderSummary } from '../../../core/api/public-models';
import { resolveOrderButtonLabel } from './order-button-label';

/**
 * ===========================================================================
 * §312j Abs. 2 BGB — DUGMENIN HEMEN USTUNDEKI ZORUNLU OZET
 * ===========================================================================
 *
 * Yasa, siparis dugmesinin **hemen ustunde**, "acik ve anlasilir bicimde",
 * §312d Abs. 1 + Art. 246a EGBGB'deki bilgileri ister:
 *   - malin/hizmetin **temel ozellikleri**,
 *   - sozlesmenin **suresi**,
 *   - **toplam fiyat** ve tum kalemler.
 *
 * TASARIM KARARLARI (hepsi hukuki gerekcelidir):
 *  1) **OZET VE DUGME AYNI BILESENDEDIR.** Ayri bileşenler olsaydi bir gun
 *     birine bir sey eklenir, aralarina bir kutu girer ve "hemen ustunde"
 *     kosulu sessizce bozulurdu. Burada aralarina hicbir sey giremez; bir
 *     birim test de dugmenin ozetin **kardes ve hemen sonraki** ogesi
 *     oldugunu dogrular.
 *  2) **ACILIR/KAPANIR DEGILDIR.** `<details>` ya da "detaylari goster"
 *     kullanilmaz: gizlenebilen bilgi "hemen ustunde ve anlasilir" sayilmaz.
 *  3) **KALEMLER SUNUCUDAN DONER, ISTEMCI SECMEZ.** `components[]` dizisi
 *     oldugu gibi basilir; istemcinin bir kalemi "gereksiz" bulup atlamasi
 *     mumkun degildir.
 *  4) **HASH BIRLIKTE TASINIR.** Gosterilen ozetin `hash`'i rezervasyon
 *     isteginde geri gonderilir; sunucu farkli hesaplarsa 409 `SUMMARY_CHANGED`
 *     doner ve akis durur (bkz. booking sayfasi).
 *  5) Dugme metni **sunucudan** gelir (bkz. order-button-label.ts).
 */
@Component({
  selector: 'hcg-order-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <section
      class="border border-rule-strong bg-paper-raised"
      [attr.aria-labelledby]="headingId"
      data-testid="order-summary"
      [attr.data-summary-hash]="summary().hash"
    >
      <h2 id="order-summary-heading" class="border-b border-rule px-5 py-3 label-mono">
        {{ 'order.summaryTitle' | translate }}
      </h2>

      <div class="px-5 py-4">
        <!-- Temel ozellikler -->
        <dl class="grid gap-y-2" data-testid="order-essentials">
          <div class="flex items-baseline justify-between gap-4 border-b border-rule pb-2">
            <dt class="text-sm text-ink-muted">{{ 'order.roomType' | translate }}</dt>
            <dd class="text-sm" data-testid="order-room-type">
              {{ summary().essentialFeatures.roomTypeName }}
            </dd>
          </div>
          <div class="flex items-baseline justify-between gap-4 border-b border-rule pb-2">
            <dt class="text-sm text-ink-muted">{{ 'order.occupancy' | translate }}</dt>
            <dd class="numeric text-sm" data-testid="order-occupancy">{{ occupancyText() }}</dd>
          </div>
          <div class="flex items-baseline justify-between gap-4 border-b border-rule pb-2">
            <dt class="text-sm text-ink-muted">{{ 'order.board' | translate }}</dt>
            <dd class="text-sm" data-testid="order-board">{{ boardText() }}</dd>
          </div>

          <!-- Sure: tarihler + yerel giris/cikis saatleri -->
          <div class="flex items-baseline justify-between gap-4 border-b border-rule pb-2">
            <dt class="text-sm text-ink-muted">{{ 'order.duration' | translate }}</dt>
            <dd class="text-right text-sm" data-testid="order-duration">
              <span class="numeric">{{ durationText() }}</span>
              <span class="mt-0.5 block text-xs text-ink-faint" data-testid="order-times">
                {{
                  'order.checkTimes'
                    | translate
                      : {
                          from: summary().duration.checkInFromLocal,
                          until: summary().duration.checkOutUntilLocal,
                        }
                }}
              </span>
            </dd>
          </div>
        </dl>

        <!-- Kalemler: sunucunun verdigi her sey, eksiksiz -->
        <dl class="mt-4 border-t border-rule" data-testid="order-components">
          @for (component of summary().components; track component.kind + component.label) {
            <div
              class="flex items-baseline justify-between gap-4 border-b border-rule py-2"
              data-testid="order-component"
              [attr.data-kind]="component.kind"
            >
              <dt class="text-sm">
                {{ componentLabel(component.labelKey, component.label) }}
                @if (component.mandatory) {
                  <span class="ml-2 eyebrow" data-testid="order-mandatory">
                    {{ 'order.mandatory' | translate }}
                  </span>
                }
              </dt>
              <dd class="numeric text-sm">{{ money(component.amount) }}</dd>
            </div>
          }

          <div class="flex items-baseline justify-between gap-4 py-3">
            <dt class="label-mono">{{ 'order.total' | translate }}</dt>
            <dd class="numeric text-2xl" data-testid="order-total">{{ totalText() }}</dd>
          </div>
        </dl>

        <p class="text-xs text-ink-muted" data-testid="order-total-note">
          {{ inclusiveKey() | translate }}
        </p>

        <p class="mt-3 text-sm" data-testid="order-payment-note">
          {{ 'order.paymentAtProperty' | translate: { amount: totalText() } }}
        </p>
      </div>
    </section>

    <!--
      DUGME. Ozetin hemen ardindan gelir; arasina hicbir oge girmez (§312j
      Abs. 2 "unmittelbar bevor"). Genislik tam: sayfadaki tek baskin eylem.
    -->
    <button
      type="button"
      class="hcg-action hcg-action--verbatim mt-4 w-full"
      [disabled]="disabled() || busy()"
      [attr.aria-describedby]="'order-summary-heading'"
      data-testid="order-button"
      (click)="confirm.emit(label())"
    >
      {{ busy() ? ('order.submitting' | translate) : label() }}
    </button>
  `,
})
export class OrderSummary {
  readonly summary = input.required<PublicOrderSummary>();
  readonly orderButton = input.required<PublicOrderButton>();
  readonly disabled = input(false);
  readonly busy = input(false);

  /** Gosterilen dugme metnini **aynen** yayar — kanit kaydi bununla yazilir. */
  readonly confirm = output<string>();

  protected readonly headingId = 'order-summary-heading';

  private readonly language = inject(LanguageStore);
  private readonly translate = inject(TranslateService);

  /** §312j Abs. 3: metin sunucudan; `de` icin birebir `labelDe`. */
  protected readonly label = computed(() =>
    resolveOrderButtonLabel(this.orderButton(), this.language.current(), (key) =>
      this.instant(key),
    ),
  );

  protected readonly totalText = computed(() =>
    formatMoney(
      this.summary().totalPrice.amount,
      this.summary().totalPrice.currency,
      this.language.current(),
    ),
  );

  protected readonly occupancyText = computed(() => {
    const occupancy = this.summary().essentialFeatures.occupancy;
    const rooms = this.summary().essentialFeatures.roomCount;
    return this.instant('order.occupancyValue', {
      adults: occupancy.adults,
      children: occupancy.children,
      rooms,
    });
  });

  protected readonly boardText = computed(() => {
    const board = this.summary().essentialFeatures.board;
    const key = `order.boardValue.${board}`;
    const translated = this.instant(key);
    return translated === key ? board : translated;
  });

  protected readonly durationText = computed(() => {
    const duration = this.summary().duration;
    const language = this.language.current();
    return this.instant('order.durationValue', {
      checkIn: formatIsoDate(duration.checkIn, language),
      checkOut: formatIsoDate(duration.checkOut, language),
      nights: duration.nights,
    });
  });

  /** Kapsayicilik beyani sunucunun bayraklarindan turer, sabit metin degil. */
  protected readonly inclusiveKey = computed(() => {
    const total = this.summary().totalPrice;
    if (total.vatIncluded && total.includesMandatoryCharges) {
      return 'order.totalNoteVatAndMandatory';
    }
    return total.vatIncluded ? 'order.totalNoteVat' : 'order.totalNoteNone';
  });

  protected money(amount: number): string {
    return formatMoney(amount, this.summary().totalPrice.currency, this.language.current());
  }

  /** Kalemin adi: kendi katalogumuz varsa o, yoksa sunucunun verdigi metin. */
  protected componentLabel(labelKey: string, fallback: string): string {
    const translated = this.instant(labelKey);
    return translated === labelKey ? fallback : translated;
  }

  private instant(key: string, params?: Record<string, unknown>): string {
    this.translate.currentLang();
    const value: unknown = this.translate.instant(key, params);
    return typeof value === 'string' ? value : key;
  }
}
