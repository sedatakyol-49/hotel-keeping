/**
 * RBAC izin anahtarlari — mimari dokumani §7 ile birebir aynidir.
 * Backend bunlari JWT icinde `perm` claim'i olarak gonderir.
 */
export const PERMISSIONS = {
  HotelsView: 'Hotels.View',
  HotelsManage: 'Hotels.Manage',
  EmployeesView: 'Employees.View',
  EmployeesEdit: 'Employees.Edit',
  VacationsView: 'Vacations.View',
  VacationsRequest: 'Vacations.Request',
  VacationsApprove: 'Vacations.Approve',
  TimeTrackingView: 'TimeTracking.View',
  TimeTrackingRecord: 'TimeTracking.Record',
  ShiftsView: 'Shifts.View',
  ShiftsEdit: 'Shifts.Edit',
  RoomsView: 'Rooms.View',
  RoomsManage: 'Rooms.Manage',
  HousekeepingView: 'Housekeeping.View',
  HousekeepingUpdate: 'Housekeeping.Update',
  ReservationsView: 'Reservations.View',
  ReservationsCreate: 'Reservations.Create',
  ReservationsCheckInOut: 'Reservations.CheckInOut',
  RatesView: 'Rates.View',
  RatesManage: 'Rates.Manage',
  InvoicesView: 'Invoices.View',
  InvoicesCreate: 'Invoices.Create',
  InvoicesApprove: 'Invoices.Approve',
  InvoicesCancel: 'Invoices.Cancel',
  ReportsView: 'Reports.View',
  SettingsManage: 'Settings.Manage',
} as const;

export type PermissionKey = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

export const ALL_PERMISSION_KEYS: readonly PermissionKey[] = Object.freeze(
  Object.values(PERMISSIONS) as PermissionKey[],
);

/** Izin kontrolu modu: tek bir izin yeterli mi, yoksa hepsi mi gerekli. */
export type PermissionMatchMode = 'any' | 'all';
