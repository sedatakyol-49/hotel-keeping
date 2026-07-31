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
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore, formatMoney } from '@hotelcore/shared';

import type { PublicAvailabilityQuery, PublicOffer } from '../../core/api/public-models';
import type { PublicErrorRecovery } from '../../core/api/public-error';
import { isIsoDate } from '../../core/dates/stay-dates';
import { languagePath } from '../../core/i18n/language-url';
import { HoldStore } from '../../core/state/hold.store';
import { HotelStore } from '../../core/state/hotel.store';
import { SearchStore } from '../../core/state/search.store';
import { ErrorPanel } from '../../shared/ui/error-panel/error-panel';
import { Notice } from '../../shared/ui/notice/notice';
import { OfferCard } from '../../shared/ui/offer-card/offer-card';
import { PageIntro } from '../../shared/ui/page-intro/page-intro';
import { SearchForm } from '../../shared/ui/search-form/search-form';

/**
 * ===========================================================================
 * ARAMA SONUCLARI
 * ===========================================================================
 *
 * SORGU ADRESTEDIR, DURUMDA DEGIL. `?checkIn=…&checkOut=…&adults=…` — boylece
 * sonuc sayfasi paylasilabilir, geri/ileri tuslari calisir ve sunucu ayni
 * sayfayi render edebilir. Sayfa `noindex, follow` tasir (rota `data`).
 *
 * BOS SONUC BIR HATA DEGILDIR. Sozlesme §4.1 hicbir tip musait olmadiginda da
 * **200** doner ve `unavailableRoomTypes[].reason` ile **neden** oldugunu
 * soyler. Ekran bu sebebi kullanilabilir bir oneriye cevirir ("kapasite yetmedi
 * -> kisi sayisini dusurun", "asgari gece sarti -> tarihleri genisletin").
 * Kuru bir "sonuc bulunamadi" kullaniciyi cikmaza sokardi.
 *
 * FIYAT: kartlardaki tutar KDV **ve** Kurtaxe dahil toplamdir (PAngV). Kurtaxe
 * "girişte ayrica" diye dipnota atilmaz; formun altinda da bilgi notu durur.
 */
@Component({
  selector: 'hcg-search-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, PageIntro, SearchForm, OfferCard, ErrorPanel, Notice],
  template: `
    <div class="hcg-shell py-12">
      <hcg-page-intro
        [eyebrow]="'search.eyebrow' | translate"
        [heading]="'search.title' | translate"
        [lede]="'search.lede' | translate"
      />

      <div class="mt-8">
        <hcg-search-form
          [initial]="query()"
          [maxAdults]="limits().maxAdults"
          [maxChildren]="limits().maxChildren"
          (submitted)="applySearch($event)"
        />
      </div>

      @if (cityTax(); as tax) {
        @if (tax.applies) {
          <p class="mt-3 text-xs text-ink-faint" data-testid="search-city-tax-note">
            {{
              'search.cityTaxNote'
                | translate: { amount: money(tax.perPersonNight, tax.currency) }
            }}
          </p>
        }
      }

      @if (!hasQuery()) {
        <div class="mt-10">
          <hcg-notice [heading]="'search.prompt.title' | translate">
            {{ 'search.prompt.body' | translate }}
          </hcg-notice>
        </div>
      } @else if (store.loading()) {
        <p class="mt-10 label-mono text-ink-muted" role="status" data-testid="search-loading">
          {{ 'common.loading' | translate }}
        </p>
      } @else if (store.error(); as error) {
        <div class="mt-10">
          <hcg-error-panel [error]="error" (recover)="recover(error.recovery)" />
        </div>
      } @else if (store.state().status === 'ready') {
        <div class="mt-10">
          <div class="flex flex-wrap items-baseline justify-between gap-4">
            <h2 class="text-headline">{{ 'search.results.title' | translate }}</h2>
            <p class="numeric text-sm text-ink-muted" data-testid="search-summary">
              {{
                'search.results.summary'
                  | translate
                    : {
                        nights: store.result()?.nights ?? 0,
                        adults: store.result()?.adults ?? 0,
                        children: store.result()?.children ?? 0,
                      }
              }}
            </p>
          </div>

          @if (holdError(); as error) {
            <div class="mt-6">
              <hcg-error-panel [error]="error" (recover)="recover(error.recovery)" />
            </div>
          }

          @if (store.emptyResult()) {
            <div class="mt-8" data-testid="search-empty">
              <hcg-notice tone="warning" [heading]="'search.empty.title' | translate">
                {{ 'search.empty.body' | translate }}
              </hcg-notice>
            </div>
          } @else {
            <div class="mt-2">
              @for (offer of store.offers(); track offer.roomTypeCode) {
                <hcg-offer-card
                  [offer]="offer"
                  [nights]="store.result()?.nights ?? 0"
                  [queryParams]="queryParams()"
                  [busy]="selecting() === offer.roomTypeCode"
                  (choose)="choose($event)"
                />
              }
            </div>
          }

          @if (store.unavailable().length > 0) {
            <section class="mt-12 border-t border-rule pt-8" data-testid="search-unavailable">
              <h3 class="label-mono text-ink-muted">
                {{ 'search.unavailable.title' | translate }}
              </h3>
              <ul class="mt-4 grid gap-3">
                @for (item of store.unavailable(); track item.roomTypeCode) {
                  <li
                    class="flex flex-wrap items-baseline justify-between gap-2 border-b border-rule pb-2"
                  >
                    <span class="text-sm">{{ item.name }}</span>
                    <span
                      class="text-xs text-ink-muted"
                      [attr.data-testid]="'unavailable-' + item.roomTypeCode"
                    >
                      {{ 'search.unavailable.reason.' + item.reason | translate }}
                    </span>
                  </li>
                }
              </ul>
            </section>
          }
        </div>
      }
    </div>
  `,
})
export class SearchPage {
  /** Sorgu parametreleri rota baglamasiyla gelir (`withComponentInputBinding`). */
  readonly checkIn = input('');
  readonly checkOut = input('');
  readonly adults = input('');
  readonly children = input('');

  protected readonly store = inject(SearchStore);
  private readonly hotel = inject(HotelStore);
  private readonly hold = inject(HoldStore);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageStore);

  protected readonly limits = this.hotel.limits;
  protected readonly cityTax = this.hotel.cityTax;
  protected readonly holdError = this.hold.error;

  private readonly pendingCode = signal<string | null>(null);

  /** Hold olusturulan oda tipi — dugmesi "islemde" gorunsun. */
  protected readonly selecting = computed(() =>
    this.hold.loading() ? this.pendingCode() : null,
  );

  /** Adresteki sorgu gecerliyse bir `PublicAvailabilityQuery` olusturur. */
  protected readonly query = computed<PublicAvailabilityQuery | null>(() => {
    const checkIn = this.checkIn();
    const checkOut = this.checkOut();
    if (!isIsoDate(checkIn) || !isIsoDate(checkOut)) {
      return null;
    }
    const adults = Number(this.adults());
    const children = Number(this.children());
    return {
      checkIn,
      checkOut,
      adults: Number.isFinite(adults) && adults > 0 ? Math.floor(adults) : 2,
      children: Number.isFinite(children) && children >= 0 ? Math.floor(children) : 0,
    };
  });

  protected readonly hasQuery = computed(() => this.query() !== null);

  protected readonly queryParams = computed<Record<string, string> | null>(() => {
    const query = this.query();
    return query === null
      ? null
      : {
          checkIn: query.checkIn,
          checkOut: query.checkOut,
          adults: String(query.adults),
          children: String(query.children),
        };
  });

  constructor() {
    this.hotel.load();

    effect(() => {
      const query = this.query();
      if (query !== null) {
        this.store.search(query);
      }
    });
  }

  protected applySearch(query: PublicAvailabilityQuery): void {
    void this.router.navigate([languagePath(this.language.current(), 'search')], {
      queryParams: {
        checkIn: query.checkIn,
        checkOut: query.checkOut,
        adults: query.adults,
        children: query.children,
      },
    });
  }

  /**
   * Teklif secildi -> hold. Basarili olursa rezervasyon adimina gecilir;
   * hold token'i adreste tasinir (yenilemede kaybolmasin) ve ayrica oturum
   * deposuna yazilir (§25 zorunlu depolama istisnasi).
   */
  protected choose(offer: PublicOffer): void {
    const query = this.query();
    if (query === null) {
      return;
    }
    this.pendingCode.set(offer.roomTypeCode);

    this.hold.create(
      {
        roomTypeCode: offer.roomTypeCode,
        checkIn: query.checkIn,
        checkOut: query.checkOut,
        adults: query.adults,
        children: query.children,
      },
      (hold) => {
        this.pendingCode.set(null);
        void this.router.navigate([languagePath(this.language.current(), 'booking')], {
          queryParams: { holdToken: hold.holdToken },
        });
      },
    );
  }

  /** Hata panelindeki tek dugme: her kurtarma yolu buradan gecer. */
  protected recover(recovery: PublicErrorRecovery): void {
    switch (recovery) {
      case 'backToSearch':
      case 'changeDates':
      case 'retry':
        this.store.retry();
        break;
      default:
        this.store.retry();
    }
  }

  protected money(amount: number, currency: string): string {
    return formatMoney(amount, currency, this.language.current());
  }
}
