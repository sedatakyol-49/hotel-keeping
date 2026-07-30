using System.Net.Http.Headers;
using HotelCore.Api.Services;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Ayarlar (Hotels / Head Office) ve Personel (Departments / Employees) integration testleri icin
/// <b>test basina</b> izole veri sahnesi.
/// <para>
/// <b>Sahne:</b> iki marka. <i>Marka A</i> iki otel tasir (<see cref="HotelAId"/>,
/// <see cref="HotelBId"/>) ve her ikisinde birer departman vardir; <i>Marka B</i>
/// (<see cref="OtherBrandHeadOfficeId"/>) tek otel tasir. Iki marka olmadan "allHotels yetkisi
/// marka sinirini asmaz" iddiasi kendiliginden yesil gorunurdu.
/// </para>
/// <para>
/// <b>Idempotentlik:</b> her sahne kendi Head Office'lerini, otellerini ve kullanicilarini GUID
/// ekli benzersiz adlarla olusturur; <see cref="DisposeAsync"/> icinde hepsi FK sirasina uygun
/// sekilde <b>fiziksel olarak</b> silinir (soft-delete edilmis satirlar dahil). Boylece testler
/// ayni veritabaninda tekrar tekrar kosabilir, iki ardisik tam kosu artik satir birakmaz ve her
/// sahnenin kendi oteli oldugu icin departman adi / personel numarasi gibi otel ici benzersiz
/// degerler testler arasinda catismaz.
/// </para>
/// <para>
/// <b>Token uretimi:</b> JWT'ler uygulamanin kendi <see cref="IJwtTokenService"/> servisiyle, test
/// konfigurasyonundaki <c>Jwt:Secret</c> ile imzalanir (bkz. <see cref="HotelCoreApiFactory"/>);
/// claim semasi uretimdekiyle birebir aynidir. <b>Ayrica</b> her token icin gercek bir
/// <see cref="User"/> satiri ve istenen oteller icin <see cref="UserHotelAccess"/> satirlari
/// yazilir: <c>/hotels</c> uclarinda erisim JWT claim'i degil <b>veritabani</b> esas alinarak
/// dogrulanir, bu yuzden yalnizca claim uretmek yeterli olmazdi.
/// </para>
/// </summary>
internal sealed class SettingsAndPersonnelScenario : IAsyncDisposable
{
    private readonly PostgresFixture _fixture;
    private readonly List<HttpClient> _clients = [];
    private readonly List<Guid> _userIds = [];

    private SettingsAndPersonnelScenario(PostgresFixture fixture, string suffix)
    {
        _fixture = fixture;
        Suffix = suffix;
    }

    /// <summary>Bu sahneye ozgu benzersiz sonek (global unique kisitlarini asmak icin).</summary>
    public string Suffix { get; }

    /// <summary>Marka A'nin Head Office'i — testlerin varsayilan markasi.</summary>
    public Guid HeadOfficeId { get; private set; }

    /// <summary>A oteli — testlerin varsayilan oteli (marka A).</summary>
    public Guid HotelAId { get; private set; }

    /// <summary>B oteli — ayni markada, tenant izolasyonunun "digeri".</summary>
    public Guid HotelBId { get; private set; }

    /// <summary>Baska markanin Head Office'i.</summary>
    public Guid OtherBrandHeadOfficeId { get; private set; }

    /// <summary>Baska markanin oteli — hicbir kosulda gorulmemelidir.</summary>
    public Guid OtherBrandHotelId { get; private set; }

    /// <summary>A otelindeki hazir departman ("Rezeption").</summary>
    public Guid DepartmentAId { get; private set; }

    /// <summary>B otelindeki hazir departman.</summary>
    public Guid DepartmentBId { get; private set; }

    /// <summary>A otelindeki oda tipi (<c>roomCount</c> alanini dogrulamak icin).</summary>
    public Guid RoomTypeAId { get; private set; }

    public static async Task<SettingsAndPersonnelScenario> StartAsync(PostgresFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await fixture.EnsureMigratedAsync();

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var scenario = new SettingsAndPersonnelScenario(fixture, suffix);

        var brandA = new HeadOffice { BrandName = $"IT Marka A {suffix}", DefaultCulture = "de" };
        var brandB = new HeadOffice { BrandName = $"IT Marka B {suffix}", DefaultCulture = "de" };
        var hotelA = NewHotel(brandA.Id, $"IT Hotel A {suffix}");
        var hotelB = NewHotel(brandA.Id, $"IT Hotel B {suffix}");
        var otherBrandHotel = NewHotel(brandB.Id, $"IT Fremdmarke {suffix}");

        var departmentA = new Department
        {
            HotelId = hotelA.Id,
            Name = "Rezeption",
            Description = "Empfang"
        };

        var departmentB = new Department { HotelId = hotelB.Id, Name = "Rezeption B" };

        var roomTypeA = new RoomType
        {
            HotelId = hotelA.Id,
            Code = "DBL",
            Name = "Doppelzimmer",
            BasePrice = 120m,
            Capacity = 2
        };

        await using var database = fixture.CreateDbContext();
        database.HeadOffices.Add(brandA);
        database.HeadOffices.Add(brandB);
        database.Hotels.Add(hotelA);
        database.Hotels.Add(hotelB);
        database.Hotels.Add(otherBrandHotel);
        database.Departments.Add(departmentA);
        database.Departments.Add(departmentB);
        database.RoomTypes.Add(roomTypeA);
        await database.SaveChangesAsync();

        scenario.HeadOfficeId = brandA.Id;
        scenario.OtherBrandHeadOfficeId = brandB.Id;
        scenario.HotelAId = hotelA.Id;
        scenario.HotelBId = hotelB.Id;
        scenario.OtherBrandHotelId = otherBrandHotel.Id;
        scenario.DepartmentAId = departmentA.Id;
        scenario.DepartmentBId = departmentB.Id;
        scenario.RoomTypeAId = roomTypeA.Id;

        return scenario;
    }

    /// <summary>
    /// Verilen izin kumesiyle imzalanmis token tasiyan istemci uretir ve token'in arkasinda
    /// gercek bir kullanici + otel erisim satiri birakir.
    /// </summary>
    /// <param name="permissions">
    /// <c>perm</c> claim'leri. Policy adi = izin anahtari oldugu icin bir izni LISTEDEN
    /// CIKARMAK ilgili ucta 403 uretmenin tek dogru yoludur.
    /// </param>
    /// <param name="hotelIds">
    /// <c>hotel</c> claim'leri ve <see cref="UserHotelAccess"/> satirlari. Sira anlamlidir: ilk
    /// otel varsayilan aktif oteldir. Verilmezse yalnizca A oteli.
    /// </param>
    /// <param name="canAccessAllHotels">Head Office bypass'i (<c>allHotels</c>).</param>
    /// <param name="activeHotelId">Verilirse <c>X-Hotel-Id</c> header'i olarak gonderilir.</param>
    /// <param name="headOfficeId">
    /// <c>headOfficeId</c> claim'i; verilmezse marka A. Baska markanin kullanicisini taklit etmek
    /// icin <see cref="OtherBrandHeadOfficeId"/> gecilir.
    /// </param>
    public async Task<HttpClient> CreateClientAsync(
        IReadOnlyList<string> permissions,
        IReadOnlyList<Guid>? hotelIds = null,
        bool canAccessAllHotels = false,
        Guid? activeHotelId = null,
        Guid? headOfficeId = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var brandId = headOfficeId ?? HeadOfficeId;
        var accessibleHotelIds = hotelIds ?? [HotelAId];
        var userId = await AddUserAsync(brandId, accessibleHotelIds);

        var descriptor = new AccessTokenDescriptor(
            UserId: userId,
            Email: $"it-{userId:N}@hotelcore.test",
            HeadOfficeId: brandId,
            Culture: "de",
            Permissions: permissions,
            HotelIds: accessibleHotelIds,
            CanAccessAllHotels: canAccessAllHotels);

        using var scope = _fixture.Api.Services.CreateScope();
        var accessToken = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .CreateAccessToken(descriptor);

        var client = _fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken.Value);

        if (activeHotelId is Guid hotelId)
        {
            client.DefaultRequestHeaders.Add(CurrentUser.HotelHeaderName, hotelId.ToString());
        }

        _clients.Add(client);

        return client;
    }

    /// <summary>Token tasimayan istemci (401 senaryolari icin).</summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = _fixture.Api.CreateClient();
        _clients.Add(client);

        return client;
    }

    /// <summary>API'yi hic kullanmadan dogrudan veritabanina departman ekler.</summary>
    public async Task<Guid> AddDepartmentAsync(Guid hotelId, string name, string? description = null)
    {
        var department = new Department
        {
            HotelId = hotelId,
            Name = name,
            Description = description
        };

        await using var database = _fixture.CreateDbContext();
        database.Departments.Add(department);
        await database.SaveChangesAsync();

        return department.Id;
    }

    /// <summary>API'yi hic kullanmadan dogrudan veritabanina calisan ekler.</summary>
    public async Task<Guid> AddEmployeeAsync(
        Guid hotelId,
        Guid departmentId,
        string firstName,
        string lastName,
        string? staffNumber = null,
        EmploymentType employmentType = EmploymentType.FullTime,
        DateOnly? hiredOn = null,
        DateOnly? terminatedOn = null,
        string? email = null,
        decimal annualLeaveDays = 28m)
    {
        var employee = new Employee
        {
            HotelId = hotelId,
            DepartmentId = departmentId,
            FirstName = firstName,
            LastName = lastName,
            StaffNumber = staffNumber,
            EmploymentType = employmentType,
            AnnualLeaveDays = annualLeaveDays,
            HiredOn = hiredOn ?? new DateOnly(2024, 3, 1),
            TerminatedOn = terminatedOn,
            Email = email
        };

        await using var database = _fixture.CreateDbContext();
        database.Employees.Add(employee);
        await database.SaveChangesAsync();

        return employee.Id;
    }

    /// <summary>API'yi hic kullanmadan dogrudan veritabanina oda ekler.</summary>
    public async Task<Guid> AddRoomAsync(Guid hotelId, Guid roomTypeId, string number)
    {
        var room = new Room
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Number = number,
            Floor = 1,
            HousekeepingStatus = HousekeepingStatus.Clean
        };

        await using var database = _fixture.CreateDbContext();
        database.Rooms.Add(room);
        await database.SaveChangesAsync();

        return room.Id;
    }

    /// <summary>Global query filter'i atlayarak ham calisan satirini okur (silinmisler dahil).</summary>
    public async Task<Employee?> FindEmployeeIncludingDeletedAsync(Guid employeeId)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(employee => employee.Id == employeeId);
    }

    /// <summary>Ham departman satirini okur — departman soft-delete EDILEMEZ.</summary>
    public async Task<Department?> FindDepartmentAsync(Guid departmentId)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.Departments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(department => department.Id == departmentId);
    }

    /// <summary>
    /// Ham otel satirini okur. UTF-8 testleri icin kritik: yanit govdesi degil <b>veritabaninda
    /// saklanan metin</b> dogrulanir.
    /// </summary>
    public async Task<Hotel?> FindHotelAsync(Guid hotelId)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.Hotels
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(hotel => hotel.Id == hotelId);
    }

    /// <summary>Ham Head Office satirini okur.</summary>
    public async Task<HeadOffice?> FindHeadOfficeAsync(Guid headOfficeId)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.HeadOffices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(headOffice => headOffice.Id == headOfficeId);
    }

    /// <summary>Sahnenin urettigi tum satirlari fiziksel olarak siler (FK sirasina uygun).</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();

        if (string.IsNullOrEmpty(_fixture.ConnectionString))
        {
            return;
        }

        await using var database = _fixture.CreateDbContext();
        Guid[] hotelIds = [HotelAId, HotelBId, OtherBrandHotelId];
        Guid[] headOfficeIds = [HeadOfficeId, OtherBrandHeadOfficeId];
        var userIds = _userIds.ToArray();

        // FK sirasi: Employees -> Departments/Rooms -> RoomTypes -> UserHotelAccesses ->
        // Users -> Hotels -> HeadOffices. IgnoreQueryFilters soft-delete edilmis satirlari da
        // kapsar; aksi halde silinen calisanlar veritabaninda birikirdi.
        await database.Employees.IgnoreQueryFilters()
            .Where(employee => hotelIds.Contains(employee.HotelId)).ExecuteDeleteAsync();
        await database.Departments.IgnoreQueryFilters()
            .Where(department => hotelIds.Contains(department.HotelId)).ExecuteDeleteAsync();
        await database.Rooms.IgnoreQueryFilters()
            .Where(room => hotelIds.Contains(room.HotelId)).ExecuteDeleteAsync();
        await database.RoomTypes.IgnoreQueryFilters()
            .Where(roomType => hotelIds.Contains(roomType.HotelId)).ExecuteDeleteAsync();
        await database.UserHotelAccesses
            .Where(access => userIds.Contains(access.UserId)).ExecuteDeleteAsync();
        await database.Users.IgnoreQueryFilters()
            .Where(user => userIds.Contains(user.Id)).ExecuteDeleteAsync();
        await database.Hotels.IgnoreQueryFilters()
            .Where(hotel => hotelIds.Contains(hotel.Id)).ExecuteDeleteAsync();
        await database.HeadOffices
            .Where(headOffice => headOfficeIds.Contains(headOffice.Id)).ExecuteDeleteAsync();
    }

    private async Task<Guid> AddUserAsync(Guid headOfficeId, IReadOnlyList<Guid> hotelIds)
    {
        var user = new User
        {
            HeadOfficeId = headOfficeId,
            Email = $"it-{Guid.NewGuid():N}@hotelcore.test",
            PasswordHash = "not-a-real-hash",
            FirstName = "Integration",
            LastName = "Test",
            Culture = "de"
        };

        await using var database = _fixture.CreateDbContext();
        database.Users.Add(user);

        for (var index = 0; index < hotelIds.Count; index++)
        {
            database.UserHotelAccesses.Add(new UserHotelAccess
            {
                UserId = user.Id,
                HotelId = hotelIds[index],
                IsDefault = index == 0
            });
        }

        await database.SaveChangesAsync();
        _userIds.Add(user.Id);

        return user.Id;
    }

    private static Hotel NewHotel(Guid headOfficeId, string name) => new()
    {
        HeadOfficeId = headOfficeId,
        Name = name,
        City = "Berlin",
        Country = Country.DE,
        Currency = "EUR",
        DefaultCulture = "de",
        TaxProfile = new TaxProfile { VatRate = 19m, ReducedVatRate = 7m }
    };
}
