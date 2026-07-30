using System.Linq.Expressions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace HotelCore.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın tek DbContext'i. Üç çapraz kesen davranışı merkezî olarak uygular:
/// (1) tenant + soft-delete global query filter, (2) denetim alanlarının doldurulması,
/// (3) GoBD değiştirilemezlik guard'ı.
/// <para>
/// <see cref="ICurrentUser"/>, <see cref="IDateTimeProvider"/> ve logger OPSİYONELDİR: migration ve
/// design-time senaryolarında kimlik yoktur. Kimlik yokken tenant filtresi hiçbir satırı
/// göstermez (HotelId = null, CanAccessAllHotels = false) — yani "güvenli varsayılan" kapalıdır.
/// </para>
/// <para>
/// (4) Benzersizlik (SQLSTATE 23505) ve <b>çakışma/dışlama</b> (SQLSTATE 23P01) ihlalleri →
/// <see cref="ConflictException"/> çevirisi; böylece ön kontrolü atlatan yarış durumları 500 değil
/// 409 döner.
/// </para>
/// <para>
/// (5) Storno çiftinin geri referansı (<c>Invoice.CancelsInvoiceId</c>) tamamlanır — bkz.
/// <see cref="ReconcileStornoBackReferences"/>.
/// </para>
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUser? _currentUser;
    private readonly IDateTimeProvider? _dateTimeProvider;
    private readonly ILogger<AppDbContext>? _logger;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUser? currentUser = null,
        IDateTimeProvider? dateTimeProvider = null,
        ILogger<AppDbContext>? logger = null)
        : base(options)
    {
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
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

        // Soft-delete filtresi unutulmuş unique index varsa burada patlar (regresyon guard'ı):
        // filtresiz index, silinmiş satırı görmeyen ön kontrolle birleşince 409 yerine 500 üretir.
        SoftDeleteIndexValidator.Validate(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        PrepareChanges();

        try
        {
            return base.SaveChanges();
        }
        catch (DbUpdateException exception) when (FindConflictingViolation(exception) is { } violation)
        {
            throw ToConflictException(violation, exception);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareChanges();

        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (FindConflictingViolation(exception) is { } violation)
        {
            throw ToConflictException(violation, exception);
        }
    }

    /// <summary>
    /// PostgreSQL'in <b>çakışma</b> sınıfı hatalarını arar: benzersizlik ihlali (23505) ve dışlama
    /// kısıtı ihlali (23P01). Sarmalama derinliği sağlayıcıya göre değişebildiği için tüm inner
    /// exception zinciri taranır.
    /// <para>
    /// <b>Katman notu:</b> Npgsql tipine bağımlılık bilinçli olarak <b>Infrastructure'da</b> tutulur;
    /// Application katmanı veritabanı sağlayıcısını tanımaz (LayerDependencyTests bunu doğrular).
    /// </para>
    /// </summary>
    private static PostgresException? FindConflictingViolation(Exception exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && IsConflict(postgres.SqlState))
            {
                return postgres;
            }
        }

        return null;
    }

    private static bool IsConflict(string? sqlState) =>
        sqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation;

    /// <summary>
    /// Savunma katmanı: ön kontrol ile INSERT arasındaki yarış durumunda (iki eşzamanlı istek)
    /// kullanıcı 500 değil <b>409</b> alır.
    /// <para>
    /// İstemciye giden metin <see cref="Messages"/> üzerinden isteğin dilinde üretilir ve
    /// <b>kısıt/tablo adı içermez</b> (şema detayı sızmaz); bu ayrıntı teşhis için yalnızca
    /// log'a yazılır. Kullanıcı dostu, alana özgü mesajı handler'ların ön kontrolü verir —
    /// buraya yalnızca o kontrolü atlatan yarış durumları düşer.
    /// </para>
    /// </summary>
    private ConflictException ToConflictException(PostgresException violation, DbUpdateException exception)
    {
        var constraint = violation.ConstraintName ?? "(bilinmiyor)";
        var table = violation.TableName ?? "(bilinmiyor)";

        if (violation.SqlState is PostgresErrorCodes.ExclusionViolation)
        {
            _logger?.ExclusionConstraintViolation(constraint, table, exception);

            return new ConflictException(Messages.ExclusionViolation, exception);
        }

        _logger?.UniqueConstraintViolation(constraint, table, exception);

        return new ConflictException(Messages.UniqueViolation, exception);
    }

    /// <summary>
    /// Kaydetmeden önceki ortak boru hattı. Sıra önemlidir: önce silme yumuşatılır
    /// (Deleted -> Modified), storno çiftinin geri referansı tamamlanır (kendisi de bir
    /// değişiklik ürettiği için denetim alanlarından ÖNCE), sonra denetim alanları yazılır,
    /// en son GoBD guard'ı nihai değişiklik kümesini denetler.
    /// </summary>
    private void PrepareChanges()
    {
        ApplySoftDelete();
        ReconcileStornoBackReferences();
        ApplyAuditInformation();
        BumpConcurrencyTokens();
        EnforceInvoiceImmutability();
    }

    /// <summary>
    /// Storno çiftinin <b>ikinci yarısını</b> tamamlar: bir fatura
    /// <c>CancelledByInvoiceId = storno.Id</c> ile iptal edildiğinde, aynı değişiklik kümesindeki
    /// storno kaydına <c>CancelsInvoiceId = orijinal.Id</c> yazılır.
    /// <para>
    /// <b>Neden burada:</b> "A.CancelledByInvoiceId = B.Id ⟺ B.CancelsInvoiceId = A.Id" değişmezi
    /// <b>iki satırı</b> ilgilendirir; PostgreSQL'de bunu bildirimsel olarak ifade eden bir kısıt
    /// yoktur (CHECK yalnızca kendi satırını görür, FK yalnızca varlığı doğrular). Tek veri düzeyi
    /// alternatifi trigger yazmaktı; trigger domain kuralını görünmez biçimde ikizler ve EF
    /// tarafında takip edilen nesnelerle senkronizasyonu bozar. Bu yüzden değişmezin sahibi
    /// <c>Invoice.MarkCancelled(Invoice)</c> domain metodudur; buradaki uzlaştırma yalnızca
    /// <b>güvenlik ağıdır</b> — yalnızca kimliği alan aşırı yükleme kullanıldığında (bugün
    /// Application katmanı böyle çağırıyor) devreye girer.
    /// </para>
    /// <para>
    /// <b>Ters yön kasıtlı olarak zorlanmaz</b> ("CancelsInvoiceId dolu ⇒ orijinal iptal edilmiş"):
    /// storno akışı iki transaction'dır — iptal faturası ilk adımda <i>taslak</i> olarak yazılır,
    /// orijinalin iptali ancak ikinci adımda commit edilir. Yani "orijinali henüz iptal edilmemiş
    /// storno taslağı" meşru bir ara durumdur.
    /// </para>
    /// </summary>
    private void ReconcileStornoBackReferences()
    {
        var invoices = ChangeTracker.Entries<Invoice>().ToList();

        foreach (var entry in invoices)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.Entity.CancelledByInvoiceId is not Guid cancellationInvoiceId)
            {
                continue;
            }

            // Yalnızca bu kaydetmede KURULAN bağlantılar uzlaştırılır; mevcut satırların
            // yeniden yazılması (ve kesinleşmiş storno'ya dokunulması) önlenir.
            if (entry.State is EntityState.Modified
                && !entry.Property(invoice => invoice.CancelledByInvoiceId).IsModified)
            {
                continue;
            }

            var storno = invoices.Find(candidate => candidate.Entity.Id == cancellationInvoiceId);
            if (storno is not null)
            {
                storno.Entity.LinkCancelledInvoice(entry.Entity.Id);
                continue;
            }

            EnsurePersistedStornoPointsBack(cancellationInvoiceId, entry.Entity.Id);
        }
    }

    /// <summary>
    /// İptal faturası bu değişiklik kümesinde takip edilmiyorsa geri referansı yazamayız (ve
    /// kesinleşmiş bir belgeyi sessizce güncellemek GoBD açısından da doğru olmaz). Bu durumda
    /// yalnızca <b>doğrulama</b> yapılır: veritabanındaki satır zaten doğru yönü gösteriyorsa
    /// sorun yoktur; aksi hâlde çağıran, çifti kuran domain metodunu kullanmaya zorlanır.
    /// </summary>
    private void EnsurePersistedStornoPointsBack(Guid cancellationInvoiceId, Guid cancelledInvoiceId)
    {
        // IgnoreQueryFilters: bütünlük denetimi tenant bağlamından bağımsız çalışmalıdır.
        var persisted = Set<Invoice>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(invoice => invoice.Id == cancellationInvoiceId)
            .Select(invoice => new { invoice.CancelsInvoiceId })
            .FirstOrDefault();

        if (persisted is null || persisted.CancelsInvoiceId == cancelledInvoiceId)
        {
            // Satır yoksa FK zaten reddeder; eşleşiyorsa değişmez korunuyor.
            return;
        }

        throw new InvalidOperationException(
            $"Storno cifti tutarsiz: {cancelledInvoiceId} kimlikli fatura {cancellationInvoiceId} " +
            "kimlikli iptal faturasina baglaniyor, fakat iptal faturasinin CancelsInvoiceId degeri " +
            $"'{persisted.CancelsInvoiceId?.ToString() ?? "null"}'. Cifti kuran domain metodunu " +
            "kullanin: Invoice.MarkCancelled(Invoice cancellationInvoice).");
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
    /// <para>
    /// <c>CancelsInvoiceId</c> bu listede <b>bilinçli olarak yok</b>: bir Stornorechnung'un neyi
    /// iptal ettiği belgenin <i>anlamının</i> parçasıdır ve düzenlenme (taslak) anında bellidir.
    /// Kesinleştikten sonra başka bir faturaya yönlendirilememesi gerekir — mevcut iptal akışı da
    /// bu alanı storno henüz taslakken yazar, dolayısıyla kısıt akışı engellemez.
    /// </para>
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
                throw new InvalidOperationException(Messages.InvoiceNotDeletable(entry.Entity.Id));
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
                    Messages.InvoiceImmutable(
                        entry.Entity.Id,
                        originalStatus,
                        string.Join(", ", blocked)));
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
                Messages.InvoiceLineItemsImmutable(
                    id,
                    status.Value,
                    entry.Entity.Id,
                    entry.State.ToString()));
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
