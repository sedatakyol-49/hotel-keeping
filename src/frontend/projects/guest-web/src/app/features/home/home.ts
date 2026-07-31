import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore, formatMoney } from '@hotelcore/shared';

import { PublicBookingApi } from '../../core/api/public-booking.api';
import { toPublicError } from '../../core/api/public-error';
import type {
  PublicAvailabilityQuery,
  PublicRoomTypeSummary,
} from '../../core/api/public-models';
import { defaultStay } from '../../core/dates/stay-dates';
import { languagePath } from '../../core/i18n/language-url';
import { transferredSlot } from '../../core/state/transferred-slot';
import { HotelStore } from '../../core/state/hotel.store';
import { MediaFrame } from '../../shared/ui/media-frame/media-frame';
import { SearchForm } from '../../shared/ui/search-form/search-form';

/**
 * Ana sayfa — akisin baslangici.
 *
 * Airbnb/Booking deseni, bizim kimligimizle: buyuk gorsel, tek net onerme ve
 * **hemen erisilebilir bir arama formu**. Yuvarlak kose, kart golgesi, ikon
 * yok; bolumler 1px cetvel ile ayrilir, sayilar mono kalir.
 *
 * KATALOG FIYATI "AB" FIYATIDIR (PAngV, sozlesme §3.1): `fromPrice.basis` her
 * zaman `BasePrice`'tir ve tarih verilmeden toplam fiyat gosterilemez. Bu
 * yuzden kartlarda "ab 120,00 € / Nacht" yazar ve etiket ayri bir satirdadir;
 * bir toplam fiyat iddiasi degildir.
 *
 * Render modu prerender: sayfa derleme aninda uretilir, katalog istemcide
 * doldurulur (fiyat degistiginde onceden uretilmis bir sayfa yalan soylemesin
 * diye tutar sunucudan gelir).
 */
@Component({
  selector: 'hcg-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, MediaFrame, SearchForm],
  template: `
    <!-- ==================== Hero ==================== -->
    <section class="hcg-shell pt-10 pb-12 lg:pt-16">
      <div class="grid items-end gap-8 lg:grid-cols-12 lg:gap-12">
        <div class="lg:col-span-5">
          <p class="eyebrow">{{ 'home.hero.eyebrow' | translate }}</p>
          <h1 class="mt-4 text-display">{{ 'home.hero.title' | translate }}</h1>
          <p class="mt-6 max-w-measure text-lede text-ink-muted">
            {{ 'home.hero.lede' | translate }}
          </p>
        </div>

        <!--
          LCP adayi: sayfanin en buyuk gorseli. "priority" yalnizca burada
          "true"; kutu 16:9 olarak onceden ayrildigi icin gercek fotograf
          gelince hicbir sey kaymaz.
        -->
        <div class="lg:col-span-7">
          <hcg-media-frame
            [width]="heroImage()?.width ?? 1600"
            [height]="heroImage()?.height ?? 900"
            [src]="heroImage()?.url ?? null"
            [priority]="true"
            [alt]="heroImage()?.alt ?? ('home.hero.imageAlt' | translate)"
            [caption]="heroImage() ? '' : ('home.hero.imageCaption' | translate)"
          />
        </div>
      </div>
    </section>

    <!-- ==================== Arama ==================== -->
    <section class="hcg-shell pb-section">
      <h2 class="sr-only">{{ 'search.form.label' | translate }}</h2>
      <hcg-search-form
        [initial]="suggestedStay()"
        [maxAdults]="limits().maxAdults"
        [maxChildren]="limits().maxChildren"
        (submitted)="startSearch($event)"
      />
    </section>

    <!-- ==================== Oda tipleri ==================== -->
    <section class="hcg-shell pb-section">
      <div class="flex flex-wrap items-baseline justify-between gap-4 border-t border-rule pt-8">
        <h2 class="text-headline">{{ 'home.rooms.title' | translate }}</h2>
        <a
          [routerLink]="searchPath()"
          class="label-mono text-ink-muted underline underline-offset-4 hover:text-copper"
        >
          {{ 'home.rooms.all' | translate }}
        </a>
      </div>

      @if (catalog.data(); as rooms) {
        <ul class="mt-10 grid gap-x-8 gap-y-12 sm:grid-cols-2 lg:grid-cols-3">
          @for (room of rooms; track room.code) {
            <li data-testid="room-teaser">
              <a [routerLink]="roomPath(room)" class="block no-underline">
                <hcg-media-frame
                  [width]="room.image?.width ?? 1200"
                  [height]="room.image?.height ?? 800"
                  [src]="room.image?.url ?? null"
                  [alt]="room.image?.alt ?? room.name"
                />
                <h3 class="mt-4 font-serif text-2xl text-ink">{{ room.name }}</h3>
              </a>
              <p class="mt-2 text-sm text-ink-muted">{{ room.shortDescription }}</p>
              <p class="mt-3 label-mono text-ink-faint">{{ facts(room) }}</p>
              <p class="mt-3" data-testid="room-price">
                <span class="eyebrow">{{ 'roomType.fromPriceLabel' | translate }}</span>
                <span class="numeric ml-2 text-lg">{{ fromPrice(room) }}</span>
                <span class="ml-2 text-xs text-ink-faint">
                  {{ 'roomType.perNight' | translate }}
                </span>
              </p>
            </li>
          }
        </ul>
      } @else {
        <ul class="mt-10 grid gap-x-8 gap-y-12 sm:grid-cols-2 lg:grid-cols-3">
          @for (placeholder of roomPlaceholders; track placeholder) {
            <li>
              <hcg-media-frame
                [width]="1200"
                [height]="800"
                [alt]="'home.rooms.imageAlt' | translate"
              />
              <p class="mt-4 label-mono text-ink-faint">{{ 'common.loading' | translate }}</p>
            </li>
          }
        </ul>
      }
    </section>

    <!-- ==================== Dogrudan rezervasyon ==================== -->
    <section class="border-t border-rule bg-paper-raised">
      <div class="hcg-shell py-section">
        <h2 class="text-headline">{{ 'home.direct.title' | translate }}</h2>
        <ul class="mt-10 grid gap-px border border-rule bg-rule lg:grid-cols-3">
          @for (benefit of benefits; track benefit.indexLabel) {
            <li class="bg-paper-raised p-6">
              <p class="eyebrow">{{ benefit.indexLabel }}</p>
              <h3 class="mt-3 font-serif text-xl">{{ benefit.titleKey | translate }}</h3>
              <p class="mt-2 text-sm text-ink-muted">{{ benefit.bodyKey | translate }}</p>
            </li>
          }
        </ul>
      </div>
    </section>
  `,
})
export class HomePage {
  private readonly language = inject(LanguageStore);
  private readonly api = inject(PublicBookingApi);
  private readonly router = inject(Router);
  private readonly hotel = inject(HotelStore);

  /* Devredilen slot — gerekce: core/state/transferred-slot.ts. */
  protected readonly catalog = transferredSlot<readonly PublicRoomTypeSummary[]>('hc.catalog');
  protected readonly limits = this.hotel.limits;

  protected readonly searchPath = computed(() => languagePath(this.language.current(), 'search'));
  protected readonly heroImage = computed(() => this.hotel.hotel()?.images[0] ?? null);

  /** Form bos acilmasin: yarindan iki gece, iki yetiskin. */
  protected readonly suggestedStay = computed<PublicAvailabilityQuery>(() => {
    const stay = defaultStay();
    return { checkIn: stay.checkIn, checkOut: stay.checkOut, adults: 2, children: 0 };
  });

  protected readonly roomPlaceholders = [0, 1, 2] as const;

  protected readonly benefits = [
    {
      indexLabel: '01',
      titleKey: 'home.direct.bestRate.title',
      bodyKey: 'home.direct.bestRate.body',
    },
    {
      indexLabel: '02',
      titleKey: 'home.direct.flexible.title',
      bodyKey: 'home.direct.flexible.body',
    },
    {
      indexLabel: '03',
      titleKey: 'home.direct.contact.title',
      bodyKey: 'home.direct.contact.body',
    },
  ] as const;

  constructor() {
    this.hotel.load();

    if (!this.catalog.adopt()) {
      this.catalog.begin();
      this.api.getRoomTypes().subscribe({
        next: (rooms) => {
          this.catalog.succeed(rooms);
          this.catalog.handOver(rooms);
        },
        error: (error: unknown) => this.catalog.fail(toPublicError(error)),
      });
    }
  }

  protected startSearch(query: PublicAvailabilityQuery): void {
    void this.router.navigate([this.searchPath()], {
      queryParams: {
        checkIn: query.checkIn,
        checkOut: query.checkOut,
        adults: query.adults,
        children: query.children,
      },
    });
  }

  protected roomPath(room: PublicRoomTypeSummary): string {
    return languagePath(this.language.current(), 'rooms', room.code.toLowerCase());
  }

  protected facts(room: PublicRoomTypeSummary): string {
    const parts = [`${room.capacity} P.`];
    if (room.sizeSqm !== null) {
      parts.push(`${room.sizeSqm} m²`);
    }
    return parts.join(' · ');
  }

  protected fromPrice(room: PublicRoomTypeSummary): string {
    return formatMoney(room.fromPrice.amount, room.fromPrice.currency, this.language.current());
  }
}
