import { PERMISSIONS, type PermissionKey } from '../core/models/permission.model';

export interface NavItem {
  /** Router yolu (mutlak). */
  readonly path: string;
  /** i18n anahtari — `nav.*`. */
  readonly labelKey: string;
  /** Bos ise herkese acik; doluysa en az biri gereklidir. */
  readonly permissions: readonly PermissionKey[];
}

export interface NavSection {
  readonly labelKey: string;
  readonly items: readonly NavItem[];
}

/**
 * Ana gezinme yapisi. Izin anahtarlari mimari §7 ile birebir eslesir;
 * kullanicida izin yoksa baglanti hic render edilmez.
 */
export const NAV_SECTIONS: readonly NavSection[] = [
  {
    labelKey: 'nav.section.overview',
    items: [{ path: '/dashboard', labelKey: 'nav.dashboard', permissions: [] }],
  },
  {
    labelKey: 'nav.section.operations',
    items: [
      { path: '/rooms', labelKey: 'nav.rooms', permissions: [PERMISSIONS.RoomsView] },
      {
        path: '/reservations',
        labelKey: 'nav.reservations',
        permissions: [PERMISSIONS.ReservationsView],
      },
      {
        path: '/housekeeping',
        labelKey: 'nav.housekeeping',
        permissions: [PERMISSIONS.HousekeepingView],
      },
    ],
  },
  {
    labelKey: 'nav.section.staff',
    items: [
      { path: '/employees', labelKey: 'nav.employees', permissions: [PERMISSIONS.EmployeesView] },
      { path: '/vacations', labelKey: 'nav.vacations', permissions: [PERMISSIONS.VacationsView] },
      {
        path: '/time-tracking',
        labelKey: 'nav.timeTracking',
        permissions: [PERMISSIONS.TimeTrackingView],
      },
      { path: '/shifts', labelKey: 'nav.shifts', permissions: [PERMISSIONS.ShiftsView] },
    ],
  },
  {
    labelKey: 'nav.section.finance',
    items: [
      { path: '/invoices', labelKey: 'nav.invoices', permissions: [PERMISSIONS.InvoicesView] },
      { path: '/reports', labelKey: 'nav.reports', permissions: [PERMISSIONS.ReportsView] },
    ],
  },
  {
    labelKey: 'nav.section.system',
    items: [
      { path: '/settings', labelKey: 'nav.settings', permissions: [PERMISSIONS.SettingsManage] },
    ],
  },
];
