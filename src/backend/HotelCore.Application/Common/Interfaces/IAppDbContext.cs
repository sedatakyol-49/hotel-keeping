using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Common.Interfaces;

/// <summary>
/// Persistence portu. Handler'lar EF Core'a doğrudan değil bu arayüz üzerinden erişir.
/// Uygulanan DbContext, tenant (HotelId) ve soft-delete global query filter'larını
/// kendiliğinden ekler; handler'ların ayrıca filtre yazması gerekmez.
/// </summary>
public interface IAppDbContext
{
    DbSet<HeadOffice> HeadOffices { get; }

    DbSet<Hotel> Hotels { get; }

    DbSet<Department> Departments { get; }

    DbSet<Employee> Employees { get; }

    DbSet<VacationRequest> VacationRequests { get; }

    DbSet<VacationBalance> VacationBalances { get; }

    DbSet<TimeEntry> TimeEntries { get; }

    DbSet<Shift> Shifts { get; }

    DbSet<RoomType> RoomTypes { get; }

    DbSet<Room> Rooms { get; }

    DbSet<RatePlan> RatePlans { get; }

    DbSet<Guest> Guests { get; }

    DbSet<Reservation> Reservations { get; }

    DbSet<Folio> Folios { get; }

    DbSet<Invoice> Invoices { get; }

    DbSet<InvoiceLineItem> InvoiceLineItems { get; }

    DbSet<Payment> Payments { get; }

    DbSet<InvoiceAuditEntry> InvoiceAuditEntries { get; }

    DbSet<HotelInvoiceCounter> HotelInvoiceCounters { get; }

    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Role> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<UserRole> UserRoles { get; }

    DbSet<UserHotelAccess> UserHotelAccesses { get; }

    DbSet<Translation> Translations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
