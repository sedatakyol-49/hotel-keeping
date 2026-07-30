import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { LanguageStore } from '@hotelcore/shared';

import { languagePath } from '../../core/i18n/language-url';
import { MediaFrame } from '../../shared/ui/media-frame/media-frame';

/**
 * Ana sayfa — bu turun **gorsel dil ornegi**.
 *
 * Airbnb/Booking deseni: buyuk gorsel, tek net onerme, hemen altinda oda
 * secenekleri. Kimlik bizim: yuvarlak kose yok, kart golgesi yok, ikon yok;
 * bolumler 1px cetvel ile ayrilir, sayilar mono kalir.
 *
 * VERI YOK: bu turda API cagrisi yapilmaz. Oda kutulari gercek olculeriyle
 * (3:2) yer tutucudur; icerik sozlesme belgesi ciktiginda ayni kutulara girer.
 * Fiyat satirlari bilincli olarak "hazirlaniyor" metnidir — uydurma fiyat
 * gostermek, sonradan gercek fiyatla degistiginde tasarimi da yalanlar.
 */
@Component({
  selector: 'hcg-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, MediaFrame],
  template: `
    <!-- ==================== Hero ==================== -->
    <section class="hcg-shell pt-10 pb-section lg:pt-16">
      <div class="grid items-end gap-8 lg:grid-cols-12 lg:gap-12">
        <div class="lg:col-span-5">
          <p class="eyebrow">{{ 'home.hero.eyebrow' | translate }}</p>
          <h1 class="mt-4 text-display">{{ 'home.hero.title' | translate }}</h1>
          <p class="mt-6 max-w-measure text-lede text-ink-muted">
            {{ 'home.hero.lede' | translate }}
          </p>
          <div class="mt-8 flex flex-wrap gap-3">
            <a [routerLink]="searchPath()" class="hcg-action" data-testid="hero-cta">
              {{ 'home.hero.cta' | translate }}
            </a>
          </div>
        </div>

        <!--
          LCP adayi: sayfanin en buyuk gorseli. "priority" yalnizca burada
          "true"; kutu 16:9 olarak onceden ayrildigi icin gercek fotograf
          gelince hicbir sey kaymaz.
        -->
        <div class="lg:col-span-7">
          <hcg-media-frame
            [width]="1600"
            [height]="900"
            [priority]="true"
            [alt]="'home.hero.imageAlt' | translate"
            [caption]="'home.hero.imageCaption' | translate"
          />
        </div>
      </div>
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

      <ul class="mt-10 grid gap-x-8 gap-y-12 sm:grid-cols-2 lg:grid-cols-3">
        @for (placeholder of roomPlaceholders; track placeholder) {
          <li data-testid="room-teaser">
            <hcg-media-frame
              [width]="1200"
              [height]="800"
              [alt]="'home.rooms.imageAlt' | translate"
            />
            <h3 class="mt-4 font-serif text-2xl">{{ 'home.rooms.pendingTitle' | translate }}</h3>
            <p class="mt-2 text-sm text-ink-muted">{{ 'home.rooms.pendingBody' | translate }}</p>
            <p class="mt-4 numeric text-sm text-ink-faint" data-testid="room-price">
              {{ 'home.rooms.pendingPrice' | translate }}
            </p>
          </li>
        }
      </ul>
    </section>

    <!-- ==================== Dogrudan rezervasyon ==================== -->
    <section class="border-t border-rule bg-paper-raised">
      <div class="hcg-shell py-section">
        <h2 class="text-headline">{{ 'home.direct.title' | translate }}</h2>
        <ul class="mt-10 grid gap-px border border-rule bg-rule lg:grid-cols-3">
          @for (benefit of benefits; track benefit) {
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

  protected readonly searchPath = computed(() => languagePath(this.language.current(), 'search'));

  /** Uc kutu: veri gelene kadar yalnizca duzeni tasir. */
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
}
