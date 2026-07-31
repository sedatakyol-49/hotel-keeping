import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { LanguageStore } from '@hotelcore/shared';

import type { PublicOffer } from '../../../core/api/public-models';
import { languagePath } from '../../../core/i18n/language-url';
import { amenityLabel } from '../amenity-label';
import { MediaFrame } from '../media-frame/media-frame';
import { PriceBlock } from '../price-block/price-block';

/**
 * Arama sonucu karti.
 *
 * DUZEN: gorsel solda (masaustu), icerik ortada, fiyat + eylem sagda. Mobilde
 * ayni DOM alt alta akar — ikinci bir "mobil kart" bileşeni yazilmaz; ayni veri,
 * ayni sira, farkli yerlesim.
 *
 * PAngV: karttaki buyuk rakam **KDV ve Kurtaxe dahil toplam**tir (bkz.
 * `hcg-price-block`). "Sonra eklenir" ifadesi hicbir yerde gecmez.
 *
 * UWG §5 (yaniltici kitlik): "son N oda" rozeti yalnizca sunucunun verdigi
 * gercek sayi ile ve **kirpilmamis** oldugunda gosterilir. `availableUnitsCapped`
 * true ise sayi bir ust sinira dayanmistir ("5+") ve kitlik iddiasi kurulamaz.
 */
@Component({
  selector: 'hcg-offer-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, MediaFrame, PriceBlock],
  template: `
    <article
      class="grid gap-6 border-t border-rule py-8 md:grid-cols-[16rem_1fr_auto] md:gap-8"
      [attr.data-testid]="'offer-' + offer().roomTypeCode"
    >
      <div>
        <hcg-media-frame
          [width]="offer().image?.width ?? 1200"
          [height]="offer().image?.height ?? 800"
          [src]="offer().image?.url ?? null"
          [alt]="offer().image?.alt ?? offer().name"
        />
      </div>

      <div>
        <h3 class="font-serif text-2xl">
          <a
            [routerLink]="detailPath()"
            [queryParams]="queryParams()"
            class="no-underline hover:text-copper"
          >
            {{ offer().name }}
          </a>
        </h3>

        <p class="mt-1 label-mono text-ink-faint" data-testid="offer-facts">{{ facts() }}</p>

        @if (offer().shortDescription) {
          <p class="mt-3 max-w-measure text-sm text-ink-muted">{{ offer().shortDescription }}</p>
        }

        <!--
          Donanim listesi: alt cizgi/cerceve YOK. Alti cizili kisa metinler
          baglanti sanilir; ayirici olarak orta nokta yeter.
        -->
        @if (offer().amenities.length > 0) {
          <ul class="mt-3 flex flex-wrap gap-x-3 gap-y-1 text-xs text-ink-muted">
            @for (amenity of offer().amenities; track amenity; let last = $last) {
              <li>
                {{ amenityLabel(amenity) }}
                @if (!last) {
                  <span class="ml-3 text-ink-faint" aria-hidden="true">·</span>
                }
              </li>
            }
          </ul>
        }

        @if (scarcity() !== null) {
          <p class="mt-3 numeric text-xs text-brass" data-testid="offer-scarcity">
            {{ 'search.offer.remaining' | translate: { count: scarcity() } }}
          </p>
        }

        <p class="mt-3 text-xs text-ink-muted" data-testid="offer-cancellation">
          {{ cancellationKey() | translate }}
        </p>
      </div>

      <div class="md:w-56">
        <hcg-price-block [price]="offer().price" [nights]="nights()" variant="compact" />

        <button
          type="button"
          class="hcg-action mt-4 w-full"
          [disabled]="busy()"
          [attr.data-testid]="'offer-select-' + offer().roomTypeCode"
          (click)="choose.emit(offer())"
        >
          {{ (busy() ? 'search.offer.selecting' : 'search.offer.select') | translate }}
        </button>

        <!-- Ikincil baglanti da bir dokunma hedefidir: >= 44px yukseklik. -->
        <a
          [routerLink]="detailPath()"
          [queryParams]="queryParams()"
          class="mt-2 flex touch-target items-center justify-center text-xs text-ink-muted underline underline-offset-4"
          [attr.data-testid]="'offer-detail-' + offer().roomTypeCode"
        >
          {{ 'search.offer.details' | translate }}
        </a>
      </div>
    </article>
  `,
})
export class OfferCard {
  readonly offer = input.required<PublicOffer>();
  readonly nights = input(0);
  readonly busy = input(false);
  readonly queryParams = input<Record<string, string> | null>(null);

  /* `select` DEGIL: standart bir DOM olayiyla ayni adi tasiyan cikti,
     sablonda hangi olayin dinlendigini belirsiz kilar (ve lint bunu yasaklar). */
  readonly choose = output<PublicOffer>();

  private readonly language = inject(LanguageStore);
  private readonly translate = inject(TranslateService);

  protected readonly detailPath = computed(() =>
    languagePath(this.language.current(), 'rooms', this.offer().roomTypeCode.toLowerCase()),
  );

  protected readonly facts = computed(() => {
    const offer = this.offer();
    const parts = [`${offer.capacity} P.`];
    if (offer.sizeSqm !== null) {
      parts.push(`${offer.sizeSqm} m²`);
    }
    return parts.join(' · ');
  });

  /**
   * Rozet esigi: 3. Sunucu 5'te kirpiyor; kirpilmis bir degerle "son 5 oda"
   * demek dogru ama bilgisizdir (gercek sayi daha yuksek olabilir). Kirpilmamis
   * ve <= 3 olan sayi hem gercek hem anlamlidir.
   */
  protected readonly scarcity = computed(() => {
    const availability = this.offer().availability;
    if (availability.availableUnitsCapped || availability.availableUnits > 3) {
      return null;
    }
    return availability.availableUnits;
  });

  protected readonly cancellationKey = computed(() =>
    this.offer().cancellationPolicy.isFreeCancellationAvailable
      ? 'search.offer.freeCancellation'
      : 'search.offer.restrictedCancellation',
  );

  protected amenityLabel(amenity: string): string {
    return amenityLabel(this.translate, amenity);
  }
}
