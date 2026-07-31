using HotelCore.Domain.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent runtime seeder. <c>HasData</c> yerine runtime seeding tercih edildi çünkü:
/// (a) parola hash'i gibi deterministik olmayan değerler HasData ile kullanılamaz,
/// (b) her model değişikliği seed verisini migration'a sızdırmaz, (c) demo veri ortama göre koşullanabilir.
/// Aynı veritabanında defalarca çalıştırılabilir; var olan kayıtlar tekrar eklenmez.
/// <para>
/// Sorgularda <c>IgnoreQueryFilters</c> kullanılır: seeder kimlik bağlamı olmadan çalıştığı için
/// tenant filtresi aksi hâlde hiçbir satırı göremez ve mevcut kayıtları tekrar eklerdi.
/// </para>
/// </summary>
public static class DbSeeder
{
    /// <summary>Demo kayıtların sabit kimlikleri — tekrar çalıştırmada aynı satırlara işaret eder.</summary>
    private static readonly Guid DemoHeadOfficeId = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid DemoHotelId = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid DemoAdminUserId = new("33333333-3333-3333-3333-333333333333");

    private const string DemoAdminEmail = "admin@hotelcore.local";

    /// <summary>
    /// UYARI: Bu düz metin parola bilinçli olarak kaynak kodda tutulan bir DEMO değeridir ve
    /// yalnızca <c>includeDevelopmentData = true</c> iken (Development ortamı) kullanılır.
    /// Production'da bu blok hiç çalışmaz; gerçek parolalar hiçbir koşulda koda yazılmaz.
    /// </summary>
    private const string DemoAdminPassword = "Admin!23";

    /// <summary>
    /// Rol → izin matrisi (architecture.md §7). "Tüm oteller (bypass)" hakkı
    /// <see cref="Role.IsHeadOfficeLevel"/> ile temsil edilir; HotelManager kendi oteliyle sınırlıdır.
    /// </summary>
    private static readonly RoleSeed[] RoleSeeds =
    [
        new("Admin", "Sistem yöneticisi — tüm izinler", true, Permissions.All),
        new("HeadOfficeManager", "Head Office yöneticisi — konsolide görünüm", true, Permissions.All),
        new("HotelManager", "Otel müdürü — kendi otelinin tamamı", false, Permissions.All),
        new("Receptionist", "Resepsiyon — rezervasyon, check-in/out, fatura oluşturma", false,
        [
            Permissions.HotelsView,
            Permissions.VacationsView,
            Permissions.VacationsRequest,
            Permissions.TimeTrackingView,
            Permissions.TimeTrackingRecord,
            Permissions.ShiftsView,
            Permissions.RoomsView,
            Permissions.HousekeepingView,
            Permissions.HousekeepingUpdate,
            Permissions.ReservationsView,
            Permissions.ReservationsCreate,
            Permissions.ReservationsCheckInOut,
            Permissions.RatesView,
            Permissions.InvoicesView,
            Permissions.InvoicesCreate
        ]),
        new("Housekeeping", "Kat hizmetleri — finansal veri GÖRMEZ", false,
        [
            Permissions.VacationsView,
            Permissions.VacationsRequest,
            Permissions.TimeTrackingView,
            Permissions.TimeTrackingRecord,
            Permissions.ShiftsView,
            Permissions.RoomsView,
            Permissions.HousekeepingView,
            Permissions.HousekeepingUpdate
        ]),
        new("Accountant", "Muhasebe — faturalama, fiyatlandırma ve raporlar", false,
        [
            Permissions.HotelsView,
            Permissions.VacationsView,
            Permissions.VacationsRequest,
            Permissions.TimeTrackingView,
            Permissions.TimeTrackingRecord,
            Permissions.ShiftsView,
            Permissions.RoomsView,
            Permissions.ReservationsView,
            Permissions.RatesView,
            Permissions.RatesManage,
            Permissions.InvoicesView,
            Permissions.InvoicesCreate,
            Permissions.InvoicesApprove,
            Permissions.InvoicesCancel,
            Permissions.ReportsView
        ])
    ];

    private static readonly DepartmentSeed[] DepartmentSeeds =
    [
        new("Reception", "Resepsiyon / Empfang"),
        new("Housekeeping", "Kat hizmetleri / Housekeeping"),
        new("Kitchen", "Mutfak / Küche"),
        new("Management", "Yönetim / Direktion")
    ];

    /// <summary>
    /// Oda tipleri. Ad ve açıklama <b>otelin varsayılan dilindedir</b> (de) — çeviri tablosu
    /// yalnızca diğer dilleri taşır (bkz. <see cref="PublicChannelSeeder"/>). Metinler misafir
    /// sitesinde birebir görüneceği için gerçekçi Almanca yazılmıştır.
    /// </summary>
    private static readonly RoomTypeSeed[] RoomTypeSeeds =
    [
        new("SGL", "Einzelzimmer",
            "Ruhiges Einzelzimmer zum begrünten Innenhof, mit Schreibtisch, Regendusche und "
            + "kostenfreiem WLAN. Ideal für Geschäftsreisende.",
            89.00m, 1, 18, "wifi,desk,safe,airConditioning"),
        new("DBL", "Doppelzimmer",
            "Großzügiges Doppelzimmer mit Kingsize-Bett, Sitzecke und bodentiefen Fenstern zur "
            + "Chausseestraße. Nespresso-Maschine und Minibar inklusive.",
            129.00m, 2, 26, "wifi,minibar,safe,coffeeMachine,airConditioning"),
        new("SUI", "Suite",
            "Suite mit separatem Wohnbereich, Balkon und Blick über Berlin-Mitte. Für bis zu vier "
            + "Personen, mit freistehender Badewanne und Regendusche.",
            219.00m, 4, 45, "wifi,minibar,balcony,safe,bathtub,coffeeMachine")
    ];

    private static readonly RoomSeed[] RoomSeeds =
    [
        new("SGL", 1, ["101", "102", "103", "104"]),
        new("DBL", 2, ["201", "202", "203", "204", "205", "206"]),
        new("SUI", 3, ["301", "302"])
    ];

    /// <summary>
    /// Sistem verisini (izinler + roller) her ortamda, demo otel/kullanıcı verisini yalnızca
    /// <paramref name="includeDevelopmentData"/> true iken kurar.
    /// </summary>
    /// <param name="context">Hedef DbContext.</param>
    /// <param name="includeDevelopmentData">
    /// Development ortamında true. Production'da <b>false</b> olmalıdır: demo otel ve
    /// bilinen parolalı demo kullanıcı yalnızca bu bayrak açıkken oluşturulur.
    /// </param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    public static async Task SeedAsync(
        AppDbContext context,
        bool includeDevelopmentData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await SeedPermissionsAsync(context, cancellationToken).ConfigureAwait(false);
        await SeedRolesAsync(context, cancellationToken).ConfigureAwait(false);

        if (!includeDevelopmentData)
        {
            return;
        }

        await SeedDemoOrganizationAsync(context, cancellationToken).ConfigureAwait(false);
        await SeedDemoAdminUserAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedPermissionsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var existingKeys = await context.Permissions
            .Select(p => p.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = Permissions.All
            .Where(key => !existingKeys.Contains(key, StringComparer.Ordinal))
            .Select(key => new Permission
            {
                Key = key,
                Module = key.Split('.')[0],
                Description = key
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.Permissions.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedRolesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var permissionIdsByKey = await context.Permissions
            .ToDictionaryAsync(p => p.Key, p => p.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        foreach (var seed in RoleSeeds)
        {
            var role = await context.Roles
                .FirstOrDefaultAsync(r => r.Name == seed.Name, cancellationToken)
                .ConfigureAwait(false);

            if (role is null)
            {
                role = new Role
                {
                    Name = seed.Name,
                    Description = seed.Description,
                    IsHeadOfficeLevel = seed.IsHeadOfficeLevel,
                    IsSystemRole = true
                };

                context.Roles.Add(role);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            var roleId = role.Id;
            var assignedPermissionIds = await context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var missing = seed.PermissionKeys
                .Where(permissionIdsByKey.ContainsKey)
                .Select(key => permissionIdsByKey[key])
                .Where(permissionId => !assignedPermissionIds.Contains(permissionId))
                .Select(permissionId => new RolePermission { RoleId = roleId, PermissionId = permissionId })
                .ToList();

            if (missing.Count == 0)
            {
                continue;
            }

            context.RolePermissions.AddRange(missing);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Kurgusal Berlin şehir oteli. Gerçek otel verisi DEĞİLDİR; yalnızca geliştirme ve
    /// demo amaçlıdır ve konfigüre edilebilir bir örnek olarak tutulur.
    /// </summary>
    private static async Task SeedDemoOrganizationAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var headOfficeExists = await context.HeadOffices
            .AnyAsync(h => h.Id == DemoHeadOfficeId, cancellationToken)
            .ConfigureAwait(false);

        if (!headOfficeExists)
        {
            context.HeadOffices.Add(new HeadOffice
            {
                Id = DemoHeadOfficeId,
                BrandName = "HotelCore Demo Group",
                DefaultCulture = "de"
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var hotelExists = await context.Hotels
            .IgnoreQueryFilters()
            .AnyAsync(h => h.Id == DemoHotelId, cancellationToken)
            .ConfigureAwait(false);

        if (!hotelExists)
        {
            context.Hotels.Add(new Hotel
            {
                Id = DemoHotelId,
                HeadOfficeId = DemoHeadOfficeId,
                Name = "HotelCore Berlin Mitte",
                Country = Country.DE,
                City = "Berlin",
                AddressLine = "Musterstraße 1",
                PostalCode = "10117",
                Phone = "+49 30 1234567",
                Email = "info@hotelcore.local",
                DefaultCulture = "de",
                Currency = "EUR",
                TaxProfile = new TaxProfile
                {
                    // Almanya: standart KDV %19, konaklama indirimli oran %7,
                    // Kurtaxe (City Tax) kişi başı gecelik 3,00 EUR.
                    VatRate = 19.00m,
                    ReducedVatRate = 7.00m,
                    CityTaxPerPersonNight = 3.00m,
                    CityTaxEnabled = true,
                    // Almanya'da belediyelerin çoğunda reşit olmayanlar Kurtaxe'den muaftır;
                    // kurgusal Berlin demo oteli bu yaygın kuralı örnekler. Yaş sınırı hesaba
                    // GİRMEZ (rezervasyonda doğum tarihi yok), faturada muafiyetin dayanağı
                    // olarak yazdırılır ve "çocuk" sayımının operasyonel tanımıdır.
                    CityTaxExemptChildren = true,
                    CityTaxChildAgeLimit = 18
                }
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await SeedDemoDepartmentsAsync(context, cancellationToken).ConfigureAwait(false);
        await SeedDemoRoomsAsync(context, cancellationToken).ConfigureAwait(false);

        // Misafire açık kanal (slug, saat dilimi, künye, hukuki belgeler, görseller, web fiyat
        // planı). Odalar ve oda tipleri kurulduktan SONRA çalışır: görseller ve fiyat planları
        // oda tiplerine bağlanır.
        await PublicChannelSeeder
            .SeedAsync(context, DemoHeadOfficeId, DemoHotelId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task SeedDemoDepartmentsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var existingNames = await context.Departments
            .IgnoreQueryFilters()
            .Where(d => d.HotelId == DemoHotelId)
            .Select(d => d.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = DepartmentSeeds
            .Where(seed => !existingNames.Contains(seed.Name, StringComparer.Ordinal))
            .Select(seed => new Department
            {
                HotelId = DemoHotelId,
                Name = seed.Name,
                Description = seed.Description
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.Departments.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task SeedDemoRoomsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var roomTypes = await context.RoomTypes
            .IgnoreQueryFilters()
            .Where(rt => rt.HotelId == DemoHotelId)
            .ToDictionaryAsync(rt => rt.Code, rt => rt.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        var missingRoomTypes = RoomTypeSeeds
            .Where(seed => !roomTypes.ContainsKey(seed.Code))
            .Select(seed => new RoomType
            {
                HotelId = DemoHotelId,
                Code = seed.Code,
                Name = seed.Name,
                Description = seed.Description,
                BasePrice = seed.BasePrice,
                Capacity = seed.Capacity,
                SizeSqm = seed.SizeSqm,
                Amenities = seed.Amenities
            })
            .ToList();

        if (missingRoomTypes.Count > 0)
        {
            context.RoomTypes.AddRange(missingRoomTypes);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var roomType in missingRoomTypes)
            {
                roomTypes[roomType.Code] = roomType.Id;
            }
        }

        var existingNumbers = await context.Rooms
            .IgnoreQueryFilters()
            .Where(r => r.HotelId == DemoHotelId)
            .Select(r => r.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missingRooms = new List<Room>();
        foreach (var seed in RoomSeeds)
        {
            if (!roomTypes.TryGetValue(seed.RoomTypeCode, out var roomTypeId))
            {
                continue;
            }

            missingRooms.AddRange(seed.Numbers
                .Where(number => !existingNumbers.Contains(number, StringComparer.Ordinal))
                .Select(number => new Room
                {
                    HotelId = DemoHotelId,
                    RoomTypeId = roomTypeId,
                    Number = number,
                    Floor = seed.Floor,
                    HousekeepingStatus = HousekeepingStatus.Clean
                }));
        }

        if (missingRooms.Count == 0)
        {
            return;
        }

        context.Rooms.AddRange(missingRooms);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Demo yönetici kullanıcı. Parola BCrypt ile hash'lenir (düz metin saklanmaz) ve
    /// bu kullanıcı yalnızca Development seed'inde oluşturulur.
    /// </summary>
    private static async Task SeedDemoAdminUserAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var userExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Id == DemoAdminUserId || u.Email == DemoAdminEmail, cancellationToken)
            .ConfigureAwait(false);

        if (!userExists)
        {
            context.Users.Add(new User
            {
                Id = DemoAdminUserId,
                HeadOfficeId = DemoHeadOfficeId,
                Email = DemoAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoAdminPassword),
                FirstName = "System",
                LastName = "Administrator",
                Culture = "de",
                IsActive = true
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var adminRoleId = await context.Roles
            .Where(r => r.Name == "Admin")
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (adminRoleId != Guid.Empty)
        {
            var hasRole = await context.UserRoles
                .AnyAsync(ur => ur.UserId == DemoAdminUserId && ur.RoleId == adminRoleId, cancellationToken)
                .ConfigureAwait(false);

            if (!hasRole)
            {
                context.UserRoles.Add(new UserRole { UserId = DemoAdminUserId, RoleId = adminRoleId });
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        var hasHotelAccess = await context.UserHotelAccesses
            .AnyAsync(a => a.UserId == DemoAdminUserId && a.HotelId == DemoHotelId, cancellationToken)
            .ConfigureAwait(false);

        if (hasHotelAccess)
        {
            return;
        }

        context.UserHotelAccesses.Add(new UserHotelAccess
        {
            UserId = DemoAdminUserId,
            HotelId = DemoHotelId,
            IsDefault = true
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record RoleSeed(
        string Name,
        string Description,
        bool IsHeadOfficeLevel,
        IReadOnlyList<string> PermissionKeys);

    private sealed record DepartmentSeed(string Name, string Description);

    private sealed record RoomTypeSeed(
        string Code,
        string Name,
        string Description,
        decimal BasePrice,
        int Capacity,
        int SizeSqm,
        string Amenities);

    private sealed record RoomSeed(string RoomTypeCode, int Floor, IReadOnlyList<string> Numbers);
}
