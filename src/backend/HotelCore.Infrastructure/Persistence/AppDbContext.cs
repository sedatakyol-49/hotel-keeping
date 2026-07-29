using System.Linq.Expressions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace HotelCore.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın tek DbContext'i. Üç çapraz kesen davranışı merkezî olarak uygular:
/// (1) tenant + soft-delete global query filter, (2) denetim alanlarının doldurulması,
/// (3) GoBD değiştirilemezlik guard'ı.
/// <para>
/// <see cref="ICurrentUser"/> ve <see cref="IDateTimeProvider"/> OPSİYONELDİR: migration ve
/// design-time senaryolarında kimlik yoktur. Kimlik yokken tenant filtresi hiçbir satırı
/// göstermez (HotelId = null, CanAccessAllHotels = false) — yani "güvenli varsayılan" kapalıdır.
/// </para>
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUser? _currentUser;
    private readonly IDateTimeProvider? _dateTimeProvider;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUser? currentUser = null,
        IDateTimeProvider? dateTimeProvider = null)
        : base(options)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>Global query filter tarafından okunur — kimlik yoksa null.</summary>
    public Guid? CurrentHotelId => _currentUser?.HotelId;

    /// <summary>Head Office konsolide erişimi; filtre koşulunun tek bypass noktası.</summary>
    public bool CurrentUserCanAccessAllHotels => _currentUser?.CanAccessAllHotels ?? false;

    public DbSet<HeadOffice> HeadOffices => Set<HeadOffice>();

    public DbSet<Hotel> Hotels => Set<Hotel>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<VacationRequest> VacationRequests => Set<VacationRequest>();

    public DbSet<VacationBalance> VacationBalances => Set<VacationBalance>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<RoomType> RoomTypes => Set<RoomType>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RatePlan> RatePlans => Set<RatePlan>();

    public DbSet<Guest> Guests => Set<Guest>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Folio> Folios => Set<Folio>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<InvoiceAuditEntry> InvoiceAuditEntries => Set<InvoiceAuditEntry>();

    public DbSet<HotelInvoiceCounter> HotelInvoiceCounters => Set<HotelInvoiceCounter>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<UserHotelAccess> UserHotelAccesses => Set<UserHotelAccess>();

    public DbSet<Translation> Translations => Set<Translation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyGlobalQueryFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        PrepareChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Kaydetmeden önceki ortak boru hattı. Sıra önemlidir: önce silme yumuşatılır
    /// (Deleted -> Modified), sonra denetim alanları yazılır, en son GoBD guard'ı nihai
    /// değişiklik kümesini denetler.
    /// </summary>
    private void PrepareChanges()
    {
        ApplySoftDelete();
        ApplyAuditInformation();
        BumpConcurrencyTokens();
        EnforceInvoiceImmutability();
    }

    /// <summary>
    /// Fatura sayacının concurrency token'ını artırır. Böylece aynı satırı eşzamanlı güncelleyen
    /// ikinci istek <c>DbUpdateConcurrencyException</c> alır ve numara tekrarı/atlaması olmaz.
    /// </summary>
    private void BumpConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<HotelInvoiceCounter>())
        {
            if (entry.State is EntityState.Modified)
            {
                entry.Entity.Version++;
            }
        }
    }

    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = UtcNow();
        }
    }

    private void ApplyAuditInformation()
    {
        var now = UtcNow();
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added && entry.Entity is ITenantEntity tenantEntity
                && tenantEntity.HotelId == Guid.Empty && CurrentHotelId is Guid hotelId)
            {
                // Aktif otel biliniyorsa yeni kayda otomatik damgalanır.
                tenantEntity.HotelId = hotelId;
            }

            if (entry.Entity is not IAuditableEntity auditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.CreatedByUserId = userId;
                    break;
                case EntityState.Modified:
                    auditable.ModifiedAt = now;
                    auditable.ModifiedByUserId = userId;
                    break;
                default:
                    break;
            }
        }
    }

    private DateTimeOffset UtcNow() => _dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

    /// <summary>
    /// GoBD sonrası fatura üzerinde değişmesine izin verilen alanlar. Durum geçişi
    /// (Finalized -> Paid / Cancelled) ve iptal faturası bağlantısı meşrudur; tutar, satır,
    /// numara, tarih gibi içerik alanları değiştirilemez.
    /// </summary>
    private static readonly string[] GoBdMutableInvoiceProperties =
    [
        nameof(Invoice.Status),
        nameof(Invoice.CancelledByInvoiceId),
        nameof(Invoice.ModifiedAt),
        nameof(Invoice.ModifiedByUserId)
    ];

    /// <summary>
    /// GoBD değiştirilemezlik guard'ı (architecture.md §6.1). Kesinleşmiş bir faturanın
    /// içeriği veya satırları güncellenemez/silinemez; ihlal durumunda kayıt reddedilir.
    /// </summary>
    private void EnforceInvoiceImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<Invoice>())
        {
            if (entry.State is EntityState.Deleted)
            {
                // Buraya yalnızca soft-delete dönüşümünü atlatan doğrudan silme girişimleri düşer.
                throw new InvalidOperationException(
                    $"Fatura silinemez (GoBD 10 yil saklama zorunlulugu). Invoice Id: {entry.Entity.Id}.");
            }

            if (entry.State is not EntityState.Modified)
            {
                continue;
            }

            var originalStatus = entry.OriginalValues.GetValue<InvoiceStatus>(nameof(Invoice.Status));
            if (originalStatus is InvoiceStatus.Draft)
            {
                continue;
            }

            var blocked = entry.Properties
                .Where(p => p.IsModified && !GoBdMutableInvoiceProperties.Contains(p.Metadata.Name))
                .Select(p => p.Metadata.Name)
                .ToArray();

            if (blocked.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Kesinlesmis fatura degistirilemez (GoBD). Invoice Id: {entry.Entity.Id}, " +
                    $"durum: {originalStatus}, degistirilen alanlar: {string.Join(", ", blocked)}. " +
                    "Duzeltme icin iptal faturasi (Stornorechnung) olusturun.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<InvoiceLineItem>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var invoiceId = GetLineItemInvoiceId(entry);
            if (invoiceId is not Guid id)
            {
                // Henüz faturaya bağlanmamış folio satırı — serbestçe düzenlenebilir.
                continue;
            }

            var status = ResolveInvoiceStatus(id);
            if (status is null or InvoiceStatus.Draft)
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Kesinlesmis faturanin satirlari degistirilemez (GoBD). Invoice Id: {id}, " +
                $"durum: {status}, satir Id: {entry.Entity.Id}, islem: {entry.State}.");
        }
    }

    /// <summary>Silinen satırlarda güncel değer okunamayacağı için orijinal değere düşülür.</summary>
    private static Guid? GetLineItemInvoiceId(EntityEntry<InvoiceLineItem> entry)
    {
        if (entry.State is EntityState.Deleted)
        {
            return entry.OriginalValues.GetValue<Guid?>(nameof(InvoiceLineItem.InvoiceId));
        }

        return entry.Entity.InvoiceId
               ?? entry.OriginalValues.GetValue<Guid?>(nameof(InvoiceLineItem.InvoiceId));
    }

    /// <summary>
    /// Faturanın SaveChanges öncesindeki (orijinal) durumunu döndürür. Aynı transaction içinde
    /// yeni oluşturulan/kesinleşen faturaya satır yazılabilmesi için orijinal durum esas alınır.
    /// </summary>
    private InvoiceStatus? ResolveInvoiceStatus(Guid invoiceId)
    {
        var tracked = ChangeTracker.Entries<Invoice>().FirstOrDefault(e => e.Entity.Id == invoiceId);
        if (tracked is not null)
        {
            return tracked.State is EntityState.Added
                ? tracked.Entity.Status
                : tracked.OriginalValues.GetValue<InvoiceStatus>(nameof(Invoice.Status));
        }

        // Takip edilmeyen fatura: yalnızca durum alanı okunur. IgnoreQueryFilters burada veri
        // görünürlüğü için değil, bütünlük denetiminin tenant bağlamından bağımsız çalışması içindir.
        return Set<Invoice>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => (InvoiceStatus?)i.Status)
            .FirstOrDefault();
    }

    /// <summary>
    /// Tenant ve soft-delete filtrelerini reflection ile tüm uygun entity'lere uygular.
    /// Tenant bypass'ı tek noktadadır: koşula <c>|| CanAccessAllHotels</c> eklenir —
    /// çağrı yerlerinde IgnoreQueryFilters ile bypass edilmesi kasıtlı olarak engellenir
    /// (architecture.md §3).
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        var context = Expression.Constant(this);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.IsOwned() || entityType.BaseType is not null)
            {
                continue;
            }

            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(ITenantEntity).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable)
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "e");
            Expression? filter = null;

            if (isTenantScoped)
            {
                var hotelId = Expression.Convert(
                    Expression.Property(parameter, nameof(ITenantEntity.HotelId)),
                    typeof(Guid?));
                var currentHotelId = Expression.Property(context, nameof(CurrentHotelId));
                var canAccessAll = Expression.Property(context, nameof(CurrentUserCanAccessAllHotels));

                filter = Expression.OrElse(Expression.Equal(hotelId, currentHotelId), canAccessAll);
            }

            if (isSoftDeletable)
            {
                var notDeleted = Expression.Not(
                    Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));
                filter = filter is null ? notDeleted : Expression.AndAlso(notDeleted, filter);
            }

            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(filter!, parameter));
        }
    }
}
