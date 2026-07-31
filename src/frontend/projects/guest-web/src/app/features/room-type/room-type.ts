import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { LanguageStore, formatMoney } from '@hotelcore/shared';

import { PublicBookingApi } from '../../core/api/public-booking.api';
import { toPublicError } from '../../core/api/public-error';
import type { PublicAvailabilityQuery, PublicRoomTypeDetail } from '../../core/api/public-models';
import { isIsoDate } from '../../core/dates/stay-dates';
import { languagePath } from '../../core/i18n/language-url';
import { asyncSlot } from '../../core/state/async-state';
import { HoldStore } from '../../core/state/hold.store';
import { HotelStore } from '../../core/state/hotel.store';
import { SearchStore } from '../../core/state/search.store';
import { amenityLabel } from '../../shared/ui/amenity-label';
import { ErrorPanel } from '../../shared/ui/error-panel/error-panel';
import { MediaFrame } from '../../shared/ui/media-frame/media-frame';
import { Notice } from '../../shared/ui/notice/notice';
import { PageIntro } from '../../shared/ui/page-intro/page-intro';
import { PriceBlock } from '../../shared/ui/price-block/price-block';
import { SearchForm } from '../../shared/ui/search-form/search-form';

/**
 * ===========================================================================
 * ODA TIPI DETAYI — SEO'nun asil hedef sayfasi
 * ===========================================================================
 *
 * IKI DURUMU VARDIR ve ikisi **farkli fiyat iddialari** tasir:
 *
 *  A) TARIHSIZ (dogrudan gelen ziyaretci / arama motoru):
 *     `fromPrice.basis = "BasePrice"` -> ekranda **"ab 120,00 €"**. PAngV
 *     acisindan bu bir baslangic fiyatidir, toplam fiyat iddiasi DEGILDIR ve
 *     oyle etiketlenmek zorundadir (sozlesme §3.1). Sayfa bir arama formu
 *     sunar; tarih girilmeden toplam fiyat gosterilemez.
 *
 *  B) TARIHLI (arama sonucundan gelindi, sorgu adreste):
 *     Musaitlik ucundan gelen **gercek teklif** gosterilir: KDV ve Kurtaxe
 *     dahil toplam, gece sayisi, iptal kosulu ve "sec" dugmesi.
 *
 * Iki durumu tek bir "fiyat" alanina sikistirmak yaniltici olurdu; bu yuzden
 * ayri bloklar ve ayri metinler kullanilir.
 */
@Component({
  selector: 'hcg-room-type-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslatePipe,
    MediaFrame,
    PageIntro,
    PriceBlock,
    SearchForm,
    ErrorPanel,
    Notice,
  ],
  template: `
    <div class="hcg-shell py-12">
      @if (detail.error(); as error) {
        <hcg-error-panel [error]="error" (recover)="reload()" />
      } @else if (detail.data(); as room) {
        <hcg-page-intro
          [eyebrow]="'roomType.eyebrow' | translate"
          [heading]="room.name"
          [lede]="room.shortDescription"
        />

        <p class="mt-4 label-mono text-ink-faint" data-testid="room-facts">
          {{ facts(room) }}
        </p>

        <!-- Galeri: kutular API'den gelen olculerle ayrilir, CLS = 0 -->
        <div class="mt-10 grid gap-6 md:grid-cols-3">
          <div class="md:col-span-2">
            <hcg-media-frame
              [width]="primaryImage(room)?.width ?? 1600"
              [height]="primaryImage(room)?.height ?? 1067"
              [src]="primaryImage(room)?.url ?? null"
              [priority]="true"
              [alt]="primaryImage(room)?.alt ?? room.name"
            />
          </div>
          <div class="grid gap-6">
            @for (image of secondaryImages(room); track image.url) {
              <hcg-media-frame
                [width]="image.width"
                [height]="image.height"
                [src]="image.url"
                [alt]="image.alt"
              />
            }
            @if (secondaryImages(room).length === 0) {
              <hcg-media-frame [width]="800" [height]="800" [alt]="room.name" />
              <hcg-media-frame [width]="800" [height]="800" [alt]="room.name" />
            }
          </div>
        </div>

        <div class="mt-12 grid gap-10 lg:grid-cols-[1fr_22rem] lg:gap-16">
          <div>
            @if (room.description) {
              <p class="max-w-measure text-lede text-ink-muted" data-testid="room-description">
                {{ room.description }}
              </p>
            }

            @if (room.amenities.length > 0) {
              <section class="mt-10 border-t border-rule pt-6">
                <h2 class="label-mono text-ink-muted">{{ 'roomType.amenities' | translate }}</h2>
                <ul class="mt-4 grid gap-x-8 gap-y-2 sm:grid-cols-2" data-testid="room-amenities">
                  @for (amenity of room.amenities; track amenity) {
                    <li class="border-b border-rule py-1.5 text-sm">{{ label(amenity) }}</li>
                  }
                </ul>
              </section>
            }

            <section class="mt-10 border-t border-rule pt-6">
              <h2 class="label-mono text-ink-muted">
                {{ 'roomType.cancellation' | translate }}
              </h2>
              <p class="mt-3 max-w-measure text-sm text-ink-muted" data-testid="room-cancellation">
                {{
                  (room.cancellationPolicy.isFreeCancellationAvailable
                    ? 'search.offer.freeCancellation'
                    : 'search.offer.restrictedCancellation'
                  ) | translate
                }}
              </p>
            </section>
          </div>

          <!-- Fiyat / rezervasyon sutunu -->
          <aside class="lg:sticky lg:top-6 lg:self-start">
            @if (offer(); as available) {
              <div class="border border-rule bg-paper-raised px-5 py-5" data-testid="room-offer">
                <hcg-price-block
                  [price]="available.price"
                  [nights]="nights()"
                  variant="full"
                />
                <button
                  type="button"
                  class="hcg-action mt-5 w-full"
                  [disabled]="hold.loading()"
                  data-testid="room-select"
                  (click)="choose(available.roomTypeCode)"
                >
                  {{ (hold.loading() ? 'search.offer.selecting' : 'search.offer.select') | translate }}
                </button>
              </div>
            } @else {
              <div class="border border-rule bg-paper-raised px-5 py-5" data-testid="room-from-price">
                <p class="eyebrow">{{ 'roomType.fromPriceLabel' | translate }}</p>
                <p class="numeric mt-1 text-3xl" data-testid="room-from-amount">
                  {{ fromPriceText(room) }}
                </p>
                <p class="mt-1 text-xs text-ink-muted" data-testid="room-from-note">
                  {{ 'roomType.fromPriceNote' | translate }}
                </p>
              </div>

              @if (unavailableReason(); as reason) {
                <div class="mt-4" data-testid="room-unavailable">
                  <hcg-notice tone="warning" [heading]="'roomType.unavailable.title' | translate">
                    {{ 'search.unavailable.reason.' + reason | translate }}
                  </hcg-notice>
                </div>
              }

              <div class="mt-6">
                <hcg-search-form
                  [initial]="query()"
                  [maxAdults]="limits().maxAdults"
                  [maxChildren]="limits().maxChildren"
                  (submitted)="applySearch($event)"
                />
              </div>
            }

            @if (hold.error(); as error) {
              <div class="mt-4">
                <hcg-error-panel [error]="error" (recover)="backToSearch()" />
              </div>
            }
          </aside>
        </div>
      } @else {
        <p class="label-mono text-ink-muted" role="status" data-testid="room-loading">
          {{ 'common.loading' | translate }}
        </p>
      }
    </div>
  `,
})
export class RoomTypePage {
  /** `/{lang}/rooms/:slug` — slug oda tipi **kodudur** (mimari §12). */
  readonly slug = input.required<string>();
  readonly checkIn = input('');
  readonly checkOut = input('');
  readonly adults = input('');
  readonly children = input('');

  private readonly api = inject(PublicBookingApi);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageStore);
  private readonly translate = inject(TranslateService);
  private readonly hotel = inject(HotelStore);
  private readonly search = inject(SearchStore);

  protected readonly hold = inject(HoldStore);
  protected readonly detail = asyncSlot<PublicRoomTypeDetail>();
  protected readonly limits = this.hotel.limits;

  private readonly requested = signal('');

  protected readonly query = computed<PublicAvailabilityQuery | null>(() => {
    const checkIn = this.checkIn();
    const checkOut = this.checkOut();
    if (!isIsoDate(checkIn) || !isIsoDate(checkOut)) {
      return null;
    }
    return {
      checkIn,
      checkOut,
      adults: Math.max(1, Number(this.adults()) || 2),
      children: Math.max(0, Number(this.children()) || 0),
    };
  });

  protected readonly nights = computed(() => this.search.result()?.nights ?? 0);

  /** Tarihli durumda gercek teklif; yoksa `null` ("ab" fiyati gosterilir). */
  protected readonly offer = computed(() => {
    if (this.query() === null) {
      return null;
    }
    return this.search.offerFor(this.slug());
  });

  protected readonly unavailableReason = computed(() => {
    if (this.query() === null || this.offer() !== null) {
      return null;
    }
    const code = this.slug().toUpperCase();
    return (
      this.search
        .unavailable()
        .find((item) => item.roomTypeCode.toUpperCase() === code)?.reason ?? null
    );
  });

  constructor() {
    this.hotel.load();

    effect(() => {
      const slug = this.slug();
      if (slug.length > 0 && this.requested() !== slug) {
        this.requested.set(slug);
        this.load(slug);
      }
    });

    effect(() => {
      const query = this.query();
      if (query !== null) {
        this.search.search(query);
      }
    });
  }

  protected reload(): void {
    this.load(this.slug());
  }

  protected applySearch(query: PublicAvailabilityQuery): void {
    void this.router.navigate([], {
      queryParams: {
        checkIn: query.checkIn,
        checkOut: query.checkOut,
        adults: query.adults,
        children: query.children,
      },
      queryParamsHandling: 'merge',
    });
  }

  protected choose(roomTypeCode: string): void {
    const query = this.query();
    if (query === null) {
      return;
    }
    this.hold.create({ roomTypeCode, ...query }, (hold) => {
      void this.router.navigate([languagePath(this.language.current(), 'booking')], {
        queryParams: { holdToken: hold.holdToken },
      });
    });
  }

  protected backToSearch(): void {
    void this.router.navigate([languagePath(this.language.current(), 'search')], {
      queryParams: this.query() ?? {},
    });
  }

  protected facts(room: PublicRoomTypeDetail): string {
    const parts = [`${room.capacity} P.`];
    if (room.sizeSqm !== null) {
      parts.push(`${room.sizeSqm} m²`);
    }
    return parts.join(' · ');
  }

  protected primaryImage(room: PublicRoomTypeDetail) {
    return room.images.length > 0 ? room.images[0] : null;
  }

  protected secondaryImages(room: PublicRoomTypeDetail) {
    return room.images.slice(1, 3);
  }

  /** PAngV: "ab" fiyati **toplam fiyat iddiasi degildir** — etiketi ayri durur. */
  protected fromPriceText(room: PublicRoomTypeDetail): string {
    return formatMoney(room.fromPrice.amount, room.fromPrice.currency, this.language.current());
  }

  protected label(amenity: string): string {
    return amenityLabel(this.translate, amenity);
  }

  private load(slug: string): void {
    this.detail.begin();
    this.api.getRoomType(slug).subscribe({
      next: (room) => this.detail.succeed(room),
      error: (error: unknown) => this.detail.fail(toPublicError(error)),
    });
  }
}
