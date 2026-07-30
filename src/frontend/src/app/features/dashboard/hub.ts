import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { CurrentHotelService } from '../../core/services/current-hotel.service';
import { AuthStore } from '../../core/state/auth.store';
import {
  NAV_SECTIONS,
  filterNavSections,
  isHubNavItem,
  type NavSummaryKind,
} from '../../layout/navigation';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { HubStore, type HubSummaryView } from './hub.store';

/** Tek bir modul karti (sablon icin hazirlanmis gorunum modeli). */
interface HubCard {
  readonly path: string;
  readonly labelKey: string;
  readonly descriptionKey: string;
  /** API'si henuz yok: soluk sunum + "hazirlaniyor" etiketi. */
  readonly planned: boolean;
  readonly summaryKind: NavSummaryKind | null;
  readonly descriptionId: string;
  readonly metaId: string;
  /** Baglantinin `aria-describedby` degeri: aciklama + ozet/durum satiri. */
  readonly describedBy: string;
}

/** Sidebar bolumune karsilik gelen kart izgarasi. */
interface HubGroup {
  readonly labelKey: string;
  readonly headingId: string;
  readonly cards: readonly HubCard[];
}

/** `/rooms/types` -> `hc-hub-rooms-types` (id'ler icin kararli slug). */
function slug(path: string): string {
  return `hc-hub-${path.replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '')}`;
}

/**
 * Hub (launcher) ekrani — `/dashboard`.
 *
 * Kenar cubugundaki modul listesi burada kart izgarasi olarak sunulur; ikisi de
 * `layout/navigation.ts` dizisinden beslenir (tek dogruluk kaynagi). Bu rotada
 * kabuk kenar cubugunu gizler (`chrome.ts` -> `HIDE_SIDEBAR`), topbar ise otel
 * ve dil secimi icin yerinde kalir.
 *
 * Canli ozetler yalnizca **gercekten var olan** uclardan gelir; API'si yazilmamis
 * moduller sayi yerine "hazirlaniyor" etiketi tasir (uydurma rakam yok).
 */
@Component({
  selector: 'hc-hub',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe, PageHeader, Button, EmptyState],
  templateUrl: './hub.html',
  styles: `
    /*
     * Kartin tamami tiklanabilir olsun diye tek bir <a> uzerine seffaf bir ortu
     * serilir ("stretched link"). Boylece kart icinde ic ice interaktif eleman
     * olusmaz: odaklanabilir tek dugum baglantinin kendisidir.
     */
    .hc-hub-card__link::after {
      content: '';
      position: absolute;
      inset: 0;
    }

    /* Odak halkasi metnin degil kartin tamaminin etrafinda cizilir. */
    .hc-hub-card__link:focus-visible {
      outline: none;
    }

    .hc-hub-card:has(.hc-hub-card__link:focus-visible) {
      outline: 2px solid var(--color-navy);
      outline-offset: -2px;
    }

    .hc-hub-card:hover {
      background-color: var(--color-paper-sunken);
    }

    /* Yukleme iskeleti: ikon yerine kayan 1px cetvel cizgisi. */
    .hc-hub-skeleton {
      position: relative;
      display: block;
      width: 4.5rem;
      height: 1px;
      overflow: hidden;
      background-color: var(--color-rule-strong);
    }

    .hc-hub-skeleton::after {
      content: '';
      position: absolute;
      inset-block: 0;
      width: 45%;
      background-color: var(--color-copper);
      animation: hc-hub-slide 1.1s linear infinite;
    }

    @keyframes hc-hub-slide {
      from {
        transform: translateX(-100%);
      }
      to {
        transform: translateX(220%);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .hc-hub-skeleton::after {
        animation: none;
        width: 100%;
      }
    }
  `,
})
export class HubPage {
  private readonly authStore = inject(AuthStore);
  protected readonly currentHotel = inject(CurrentHotelService);
  protected readonly hub = inject(HubStore);

  /**
   * Izne gore suzulmus kart gruplari. Tum kartlari suzulen bolum hic
   * dondurulmez, boylece bolum basligi da gorunmez.
   */
  protected readonly groups = computed<readonly HubGroup[]>(() =>
    filterNavSections(
      NAV_SECTIONS,
      (item) => isHubNavItem(item) && this.authStore.matchesPermissions(item.permissions),
    ).map((section) => ({
      labelKey: section.labelKey,
      headingId: slug(section.labelKey),
      cards: section.items.filter(isHubNavItem).map((item) => {
        const descriptionId = `${slug(item.path)}-desc`;
        const metaId = `${slug(item.path)}-meta`;
        return {
          path: item.path,
          labelKey: item.labelKey,
          descriptionKey: item.hub.descriptionKey,
          planned: item.hub.planned === true,
          summaryKind: item.hub.summary ?? null,
          descriptionId,
          metaId,
          describedBy: `${descriptionId} ${metaId}`,
        } satisfies HubCard;
      }),
    })),
  );

  constructor() {
    // Aktif otel degistiginde ozetler yeniden cekilir (istekler `X-Hotel-Id` tasir).
    effect(() => {
      this.currentHotel.hotelId();
      untracked(() => void this.hub.load());
    });
  }

  /** Kart basina ozet gorunumu; kaynagi olmayan modulde `null`. */
  protected summaryOf(card: HubCard): HubSummaryView | null {
    return card.summaryKind === null ? null : this.hub.summaries()[card.summaryKind];
  }

  protected refresh(): void {
    void this.hub.load();
  }
}
