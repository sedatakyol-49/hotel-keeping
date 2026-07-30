import { PERMISSIONS, type PermissionKey } from '../core/models/permission.model';

/**
 * Hub kartinda gosterilecek canli ozetin veri kaynagi.
 *
 * Yalnizca **gercekten var olan** uclar icin tanimlidir:
 * `rooms` -> `GET /rooms` + `GET /rooms/board`, `roomTypes` -> `GET /room-types`,
 * `housekeeping` -> `GET /rooms/board`. API'si henuz yazilmamis modullerde bu
 * alan bos kalir; hicbir kartta uydurma/placeholder sayi gosterilmez.
 */
export type NavSummaryKind = 'rooms' | 'roomTypes' | 'housekeeping';

/**
 * Bir modulun hub (launcher) ekranindaki sunumu. Bu alan **yoksa** modul hub
 * kart izgarasinda hic listelenmez (ornek: hub'in kendisi olan `/dashboard`).
 */
export interface NavHubMeta {
  /** Kart aciklamasi — `hub.cards.*.description`. */
  readonly descriptionKey: string;
  /** Canli ozet kaynagi; verilmezse kartta sayi satiri hic olusmaz. */
  readonly summary?: NavSummaryKind;
  /**
   * Backend ucu henuz yok: kart soluk gosterilir ve "hazirlaniyor" etiketi
   * tasir. Baglanti calismaya devam eder (modul iskeleti acilir).
   */
  readonly planned?: boolean;
}

export interface NavItem {
  /** Router yolu (mutlak). */
  readonly path: string;
  /** i18n anahtari — `nav.*`. Sidebar ve hub ayni anahtari kullanir. */
  readonly labelKey: string;
  /** Bos ise herkese acik; doluysa en az biri gereklidir. */
  readonly permissions: readonly PermissionKey[];
  /** Hub kart izgarasi icin ek ustveri; yoksa modul hub'da gosterilmez. */
  readonly hub?: NavHubMeta;
}

/**
 * Kenar cubugundaki bir **ana menu kalemi** ve onun alt menusu.
 *
 * Tek ogesi olan bolum kenar cubugunda dogrudan baglanti olarak cizilir (araya
 * gereksiz bir accordion katmani koymamak icin); coklu bolum acilip kapanan bir
 * ana kalem olur. Modul eklendikce tek ogeli bolum kendiliginden gruba doner.
 */
export interface NavSection {
  readonly labelKey: string;
  /**
   * Daraltilmis (rail) kenar cubugunda gosterilen kisa gosterim anahtari —
   * `nav.short.*`. Stok ikon/emoji kullanilmadigi icin gosterim tipografiktir
   * ve **cevrilebilir** olmalidir (DE "BE" ≠ EN "OP").
   */
  readonly shortKey: string;
  readonly items: readonly NavItem[];
}

/** `hub` ustverisi tanimli modul — daraltilmis tip. */
export type HubNavItem = NavItem & { readonly hub: NavHubMeta };

/**
 * Ana gezinme yapisi — **modul listesinin tek dogruluk kaynagi**.
 *
 * Hem kenar cubugu (`Sidebar`) hem hub kart izgarasi (`HubPage`) bu diziden
 * beslenir; yeni bir modul yalnizca burada tanimlanir. Izin anahtarlari mimari
 * §7 ile birebir eslesir; kullanicida izin yoksa ne baglanti ne kart render
 * edilir.
 */
export const NAV_SECTIONS: readonly NavSection[] = [
  {
    labelKey: 'nav.section.overview',
    shortKey: 'nav.short.overview',
    items: [
      // Hub'in kendisi: kenar cubugunda geri donus baglantisi olarak durur,
      // kart izgarasinda kendini tekrar etmez (bu yuzden `hub` ustverisi yok).
      { path: '/dashboard', labelKey: 'nav.dashboard', permissions: [] },
    ],
  },
  {
    labelKey: 'nav.section.operations',
    shortKey: 'nav.short.operations',
    items: [
      {
        path: '/rooms',
        labelKey: 'nav.rooms',
        permissions: [PERMISSIONS.RoomsView],
        hub: { descriptionKey: 'hub.cards.rooms.description', summary: 'rooms' },
      },
      {
        path: '/rooms/types',
        labelKey: 'nav.roomTypes',
        permissions: [PERMISSIONS.RoomsManage],
        hub: { descriptionKey: 'hub.cards.roomTypes.description', summary: 'roomTypes' },
      },
      {
        path: '/reservations',
        labelKey: 'nav.reservations',
        permissions: [PERMISSIONS.ReservationsView],
        hub: { descriptionKey: 'hub.cards.reservations.description', planned: true },
      },
      {
        path: '/housekeeping',
        labelKey: 'nav.housekeeping',
        permissions: [PERMISSIONS.HousekeepingView],
        hub: { descriptionKey: 'hub.cards.housekeeping.description', summary: 'housekeeping' },
      },
    ],
  },
  {
    labelKey: 'nav.section.staff',
    shortKey: 'nav.short.staff',
    items: [
      {
        path: '/employees',
        labelKey: 'nav.employees',
        permissions: [PERMISSIONS.EmployeesView],
        hub: { descriptionKey: 'hub.cards.employees.description', planned: true },
      },
      {
        path: '/vacations',
        labelKey: 'nav.vacations',
        permissions: [PERMISSIONS.VacationsView],
        hub: { descriptionKey: 'hub.cards.vacations.description', planned: true },
      },
      {
        path: '/time-tracking',
        labelKey: 'nav.timeTracking',
        permissions: [PERMISSIONS.TimeTrackingView],
        hub: { descriptionKey: 'hub.cards.timeTracking.description', planned: true },
      },
      {
        path: '/shifts',
        labelKey: 'nav.shifts',
        permissions: [PERMISSIONS.ShiftsView],
        hub: { descriptionKey: 'hub.cards.shifts.description', planned: true },
      },
    ],
  },
  {
    labelKey: 'nav.section.finance',
    shortKey: 'nav.short.finance',
    items: [
      {
        path: '/invoices',
        labelKey: 'nav.invoices',
        permissions: [PERMISSIONS.InvoicesView],
        hub: { descriptionKey: 'hub.cards.invoices.description', planned: true },
      },
      {
        path: '/reports',
        labelKey: 'nav.reports',
        permissions: [PERMISSIONS.ReportsView],
        hub: { descriptionKey: 'hub.cards.reports.description', planned: true },
      },
    ],
  },
  {
    labelKey: 'nav.section.system',
    shortKey: 'nav.short.system',
    items: [
      {
        path: '/settings',
        labelKey: 'nav.settings',
        permissions: [PERMISSIONS.SettingsManage],
        // Backend uclari hazir (GET/PUT /hotels/{id}/settings, /head-office/settings).
        hub: { descriptionKey: 'hub.cards.settings.description' },
      },
    ],
  },
];

/**
 * Bolumleri verilen yordama gore suzer. Tum ogeleri suzulen bolum hic
 * dondurulmez — boylece basligi da (sidebar'da ve hub'da) gorunmez.
 */
export function filterNavSections(
  sections: readonly NavSection[],
  keep: (item: NavItem) => boolean,
): readonly NavSection[] {
  return sections
    .map((section) => ({ ...section, items: section.items.filter(keep) }))
    .filter((section) => section.items.length > 0);
}

/** Hub kart izgarasinda yer alan modul mu (ustverisi var mi). */
export function isHubNavItem(item: NavItem): item is HubNavItem {
  return item.hub !== undefined;
}
