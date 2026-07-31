import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore, formatMoney } from '@hotelcore/shared';

import type { PublicPrice } from '../../../core/api/public-models';

/**
 * ===========================================================================
 * PAngV FIYAT GOSTERIMI — tek bilesen, her ekranda ayni
 * ===========================================================================
 *
 * Preisangabenverordnung: tuketiciye gosterilen fiyat **Gesamtpreis**'tir —
 * KDV **dahil** ve **tum zorunlu kalemler** dahil. Kurtaxe zorunlu bir
 * kalemdir; "girişte ayrica alinir" diye dipnota atilamaz. Bu yuzden:
 *
 *  - Buyuk rakam her zaman `price.totalGross`'tur (Kurtaxe icinde).
 *  - Hemen altinda "KDV ve Kurtaxe dahil" ifadesi durur — kosullu degil,
 *    `vatIncluded`/`mandatoryExtrasIncluded` alanlarindan **beyan edilerek**.
 *  - Kurtaxe ayrica **ayri satir** olarak gosterilir (mimari §9.9): tutarin
 *    nereden geldigi gorunur olmali, ama toplamdan cikarilmis gibi durmamali.
 *
 * ORTALAMA TUZAGI: fiyat gece gece degisebilir (sezon gecisi). `nightly[]`
 * degerleri esit degilse "gecelik X €" demek yaniltici olur; bu durumda
 * ortalama **acikca etiketlenir** ("ortalama"). Sozlesme §4.2 bunu emreder.
 *
 * Sayilar `numeric` (mono + tabular): fiyatlar alt alta karsilastirilir.
 */
@Component({
  selector: 'hcg-price-block',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  template: `
    <div [attr.data-testid]="'price-' + variant()">
      <p class="eyebrow">{{ 'price.total' | translate }}</p>
      <p class="numeric mt-1 text-3xl" data-testid="price-total">{{ totalText() }}</p>
      <p class="mt-1 text-xs text-ink-muted" data-testid="price-inclusive">
        {{ inclusiveKey() | translate }}
      </p>

      @if (nights() > 0) {
        <p class="mt-1 text-xs text-ink-faint" data-testid="price-nightly">
          {{
            (nightlyVaries() ? 'price.perNightAverage' : 'price.perNight')
              | translate: { amount: averageText(), nights: nights() }
          }}
        </p>
      }

      @if (variant() === 'full') {
        <dl class="mt-5 border-t border-rule" data-testid="price-breakdown">
          <div class="flex items-baseline justify-between gap-4 border-b border-rule py-2.5">
            <dt class="text-sm">
              {{ 'price.accommodation' | translate: { nights: nights() } }}
            </dt>
            <dd class="numeric text-sm" data-testid="price-accommodation">
              {{ money(price().accommodationGross) }}
            </dd>
          </div>

          @if (price().cityTax.applies) {
            <div class="flex items-baseline justify-between gap-4 border-b border-rule py-2.5">
              <dt class="text-sm">
                {{ 'price.cityTax' | translate }}
                <span class="mt-0.5 block text-xs text-ink-faint" data-testid="city-tax-basis">
                  {{
                    'price.cityTaxBasis'
                      | translate
                        : {
                            persons: price().cityTax.taxablePersons,
                            nights: price().cityTax.nights,
                            rate: money(price().cityTax.perPersonNight),
                          }
                  }}
                </span>
              </dt>
              <dd class="numeric text-sm" data-testid="price-city-tax">
                {{ money(price().cityTax.amount) }}
              </dd>
            </div>
          }

          <div class="flex items-baseline justify-between gap-4 border-b border-rule py-3">
            <dt class="label-mono">{{ 'price.total' | translate }}</dt>
            <dd class="numeric text-base" data-testid="price-total-row">{{ totalText() }}</dd>
          </div>
        </dl>

        <ul class="mt-3 grid gap-1 text-xs text-ink-faint">
          <li data-testid="price-vat-note">
            {{
              'price.vatNote'
                | translate
                  : {
                      rate: price().accommodationVatRate,
                      amount: money(price().accommodationVat),
                    }
            }}
          </li>
          @if (price().cityTax.applies) {
            <li data-testid="price-city-tax-note">{{ 'price.cityTaxNote' | translate }}</li>
            @if (price().cityTax.childExemptionApplied) {
              <li data-testid="price-child-exemption">
                {{
                  'price.cityTaxChildExemption'
                    | translate: { age: price().cityTax.childAgeLimit }
                }}
              </li>
            }
          }
          <li data-testid="price-due-note">
            {{ 'price.dueAtProperty' | translate: { amount: money(price().amountDueAtProperty) } }}
          </li>
        </ul>
      }
    </div>
  `,
})
export class PriceBlock {
  readonly price = input.required<PublicPrice>();
  readonly nights = input(0);
  readonly variant = input<'compact' | 'full'>('compact');

  private readonly language = inject(LanguageStore);

  protected readonly totalText = computed(() => this.money(this.price().totalGross));
  protected readonly averageText = computed(() => this.money(this.price().averageNightlyGross));

  /** Gece fiyatlari esit degilse ortalama oldugu **soylenmek zorundadir**. */
  protected readonly nightlyVaries = computed(() => {
    const nightly = this.price().nightly;
    return nightly.length > 1 && nightly.some((night) => night.gross !== nightly[0].gross);
  });

  /**
   * Kapsayicilik beyani sunucudan gelen bayraklardan turer; sablonda sabit
   * metin yazilmaz. Kurtaxe uygulanmiyorsa "Kurtaxe dahil" demek yanlis olur.
   */
  protected readonly inclusiveKey = computed(() => {
    const price = this.price();
    if (price.cityTax.applies && price.cityTax.includedInTotal) {
      return price.vatIncluded ? 'price.inclusiveVatCityTax' : 'price.inclusiveCityTax';
    }
    return price.vatIncluded ? 'price.inclusiveVat' : 'price.inclusiveNone';
  });

  protected money(amount: number): string {
    return formatMoney(amount, this.price().currency, this.language.current());
  }
}
