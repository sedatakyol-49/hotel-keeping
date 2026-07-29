namespace HotelCore.Domain.Common;

/// <summary>
/// Granüler izin anahtarları (architecture.md §7). Format: <c>Modül.Aksiyon</c>.
/// Bu sabitler hem seed'de <c>Permission.Key</c> olarak, hem de API tarafında
/// policy adı / <c>perm</c> claim değeri olarak kullanılır. Rol adları koda gömülmez.
/// </summary>
public static class Permissions
{
    public const string HotelsView = "Hotels.View";
    public const string HotelsManage = "Hotels.Manage";

    public const string EmployeesView = "Employees.View";
    public const string EmployeesEdit = "Employees.Edit";

    public const string VacationsView = "Vacations.View";
    public const string VacationsRequest = "Vacations.Request";
    public const string VacationsApprove = "Vacations.Approve";

    public const string TimeTrackingView = "TimeTracking.View";
    public const string TimeTrackingRecord = "TimeTracking.Record";

    public const string ShiftsView = "Shifts.View";
    public const string ShiftsEdit = "Shifts.Edit";

    public const string RoomsView = "Rooms.View";
    public const string RoomsManage = "Rooms.Manage";

    public const string HousekeepingView = "Housekeeping.View";
    public const string HousekeepingUpdate = "Housekeeping.Update";

    public const string ReservationsView = "Reservations.View";
    public const string ReservationsCreate = "Reservations.Create";
    public const string ReservationsCheckInOut = "Reservations.CheckInOut";

    public const string RatesView = "Rates.View";
    public const string RatesManage = "Rates.Manage";

    public const string InvoicesView = "Invoices.View";
    public const string InvoicesCreate = "Invoices.Create";
    public const string InvoicesApprove = "Invoices.Approve";
    public const string InvoicesCancel = "Invoices.Cancel";

    public const string ReportsView = "Reports.View";

    public const string SettingsManage = "Settings.Manage";

    /// <summary>Sistemdeki tüm izin anahtarları (seed ve policy kaydı için tek kaynak).</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        HotelsView,
        HotelsManage,
        EmployeesView,
        EmployeesEdit,
        VacationsView,
        VacationsRequest,
        VacationsApprove,
        TimeTrackingView,
        TimeTrackingRecord,
        ShiftsView,
        ShiftsEdit,
        RoomsView,
        RoomsManage,
        HousekeepingView,
        HousekeepingUpdate,
        ReservationsView,
        ReservationsCreate,
        ReservationsCheckInOut,
        RatesView,
        RatesManage,
        InvoicesView,
        InvoicesCreate,
        InvoicesApprove,
        InvoicesCancel,
        ReportsView,
        SettingsManage
    ];
}
