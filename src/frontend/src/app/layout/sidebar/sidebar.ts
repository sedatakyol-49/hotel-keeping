import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthStore } from '../../core/state/auth.store';
import { NAV_SECTIONS, filterNavSections, type NavItem } from '../navigation';
import { SidebarState } from '../sidebar-state';

/** Sablon icin hazirlanmis alt menu ogesi. */
interface SidebarLink {
  readonly path: string;
  readonly labelKey: string;
  readonly active: boolean;
}

/** Bir ana menu kalemi (alt menusuyle). */
interface SidebarGroup {
  readonly labelKey: string;
  readonly shortKey: string;
  readonly panelId: string;
  /** Tek ogeli bolum: accordion yerine dogrudan baglanti cizilir. */
  readonly single: boolean;
  /** Alt kalemlerden biri aktif mi (ana kalemi vurgulamak ve otomatik acmak icin). */
  readonly active: boolean;
  readonly items: readonly SidebarLink[];
}

/**
 * Ana gezinme: **ana menu kalemleri + acilip kapanan alt menuler**.
 *
 * Modul listesi `layout/navigation.ts` dizisinden gelir — hub kart izgarasi da
 * ayni diziyi okur, boylece yeni modul iki yerde tanimlanmaz. Bir bolumun tum
 * ogeleri izinle suzulurse bolum (ve dolayisiyla ana kalem) hic cizilmez.
 *
 * Aktif kalem `routerLinkActive` ile degil **en uzun yol eslesmesi** ile
 * bulunur: `/rooms` ve `/rooms/types` kardes kalemler oldugu icin prefix
 * eslesmesi ikisini birden aktif gosterirdi; `/rooms/new` uzerinde ise tam
 * eslesme hicbirini aktif yapmazdi. En uzun eslesme her iki durumu da dogru
 * cozer.
 *
 * Daraltilmis (rail) modda ana kalemler kisa tipografik gosterimle durur ve alt
 * menu ucan panel olarak acilir (bkz. bilesen stilleri).
 *
 * Cubugun **kendi denetimi** (daraltma dugmesi) ust blogundadir; marka isareti
 * ise ust cubugun en solunda durur (bkz. `topbar.ts`).
 */
@Component({
  selector: 'hc-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe],
  templateUrl: './sidebar.html',
  styles: `
    /*
     * Daraltma dugmesinin cizimi (ust cubuktaki menu ikonuyla ayni dil).
     * Yalnizca cizim ozellikleri; cerceve ve dokunmatik hedef sablondaki
     * cetvel dilinden gelir.
     */
    .hc-icon {
      width: 1.125rem;
      height: 1.125rem;
      stroke: currentColor;
      stroke-width: 1.25;
      /* Kesin uclar ve sivri kose: defter dilinde yuvarlatma yok. */
      stroke-linecap: butt;
      stroke-linejoin: miter;
      shape-rendering: geometricPrecision;
    }

    /*
     * Alt menu gecisi: kullanicinin gozunu yormayan kisa bir acilma.
     * grid-template-rows yaklasimi sabit yukseklik gerektirmedigi icin
     * degisken sayida alt kalemle calisir.
     */
    .hc-submenu {
      display: grid;
      grid-template-rows: 1fr;
      overflow: hidden;
    }

    .hc-submenu[hidden] {
      display: grid;
      grid-template-rows: 0fr;
    }

    .hc-submenu > li {
      min-height: 0;
    }

    @media (prefers-reduced-motion: no-preference) {
      .hc-submenu {
        transition: grid-template-rows 160ms ease-out;
      }
    }

    /* --- Daraltilmis (rail) mod: alt menu ucan panel --- */
    .hc-rail .hc-submenu {
      position: absolute;
      left: 100%;
      top: 0;
      z-index: 30;
      width: 14rem;
      border: 1px solid var(--color-rule);
      border-left: none;
      background-color: var(--color-paper-raised);
      grid-template-rows: 1fr;
      transition: none;
    }

    .hc-rail .hc-submenu[hidden] {
      display: none;
    }

    /*
     * Rail modunda fare/klavye ile de erisilebilir olmasi icin: tiklama durumu
     * korunur, ustune gelme ve odak alma paneli gecici olarak acar.
     * hidden niteligine ait UA/preflight kurali dusuk ozgullukte oldugu icin bu
     * secici onu gecer; !important gerekmez.
     */
    .hc-rail .hc-group:hover > .hc-submenu,
    .hc-rail .hc-group:focus-within > .hc-submenu {
      display: grid;
    }
  `,
})
export class Sidebar {
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly sidebarState = inject(SidebarState);

  /** Mobil cekmecede baglantiya tiklaninca kapatmak icin. */
  readonly navigated = output<void>();

  /**
   * Rail moduna girebilir mi. Mobil cekmece `false` verir: kullanici masaustunde
   * kenar cubugunu daraltmis olsa bile cekmece her zaman tam genislikte acilir
   * (daraltma masaustune ozgu bir tercih).
   */
  readonly allowCollapse = input(true);

  protected readonly collapsed = computed(
    () => this.allowCollapse() && this.sidebarState.collapsed(),
  );

  /** Sorgu/parca atilmis etkin yol; aktiflik hesabinin girdisi. */
  private readonly currentPath = signal(stripUrl(this.router.url));

  protected readonly groups = computed<readonly SidebarGroup[]>(() => {
    const path = this.currentPath();
    const sections = filterNavSections(NAV_SECTIONS, (item) =>
      this.authStore.matchesPermissions(item.permissions),
    );

    const activePath = longestMatch(
      path,
      sections.flatMap((section) => section.items),
    );

    return sections.map((section) => {
      const items = section.items.map((item) => ({
        path: item.path,
        labelKey: item.labelKey,
        active: item.path === activePath,
      }));

      return {
        labelKey: section.labelKey,
        shortKey: section.shortKey,
        panelId: `hc-nav-${section.labelKey.replace(/[^a-z0-9]+/gi, '-')}`,
        single: items.length === 1,
        active: items.some((item) => item.active),
        items,
      } satisfies SidebarGroup;
    });
  });

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((event) => this.currentPath.set(stripUrl(event.urlAfterRedirects)));

    // Aktif rotayi iceren ana kalem kendiliginden acilir (sayfa yenilemede de).
    effect(() => {
      const active = this.groups().find((group) => group.active && !group.single);
      if (active) {
        untracked(() => this.sidebarState.ensureExpanded(active.labelKey));
      }
    });
  }

  protected isOpen(groupKey: string): boolean {
    return this.sidebarState.isExpanded(groupKey);
  }

  protected toggle(groupKey: string): void {
    this.sidebarState.toggleGroup(groupKey);
  }

  /**
   * Rail moduna gecer / geri doner. Durum `SidebarState` uzerinden kalicidir;
   * kabuk ayni sinyali okuyup sutun genisligini ayarlar — bu yuzden dugmenin
   * kabuga cikip geri inen bir olay tasimasina gerek yoktur.
   */
  protected toggleCollapsed(): void {
    this.sidebarState.toggleCollapsed();
  }
}

/** `/rooms/types?page=2#x` -> `/rooms/types` */
function stripUrl(url: string): string {
  return url.split(/[?#]/)[0] ?? url;
}

/** Verilen yolu iceren en uzun (dolayisiyla en ozgul) menu yolunu bulur. */
function longestMatch(path: string, items: readonly NavItem[]): string | null {
  let best: string | null = null;

  for (const item of items) {
    const matches = path === item.path || path.startsWith(`${item.path}/`);
    if (matches && (best === null || item.path.length > best.length)) {
      best = item.path;
    }
  }

  return best;
}
