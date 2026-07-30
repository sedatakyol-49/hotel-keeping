using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Tests.Support;

/// <summary>
/// Ayarlar (Hotels / Head Office) ve Personel (Departments / Employees) modullerinin handler
/// testleri icin mini host. Ortak altyapi <see cref="ApplicationTestHost"/> icindedir.
/// <para>
/// <b>Sahne:</b> iki marka kurulur. <i>Marka A</i> (<see cref="HeadOfficeId"/>) iki otel tasir
/// (<see cref="HotelId"/> = A, <see cref="OtherHotelId"/> = B); <i>Marka B</i>
/// (<see cref="OtherBrandHeadOfficeId"/>) tek otel tasir. Boylece "allHotels yetkisi olan
/// kullanici bile baska markanin otelini gormez" kurali gercek veriyle dogrulanabilir — tek
/// markali bir sahnede bu test kendiliginden yesil gorunurdu.
/// </para>
/// <para>
/// <b>Kullanici satiri:</b> <c>HotelReader</c> erisimi JWT claim'i yerine
/// <see cref="UserHotelAccess"/> tablosundan dogrular. Bu yuzden host, kimlikteki
/// <c>UserId</c> ile ayni Id'ye sahip gercek bir <see cref="User"/> satiri seed eder ve
/// erisim <see cref="GrantHotelAccessAsync"/> ile verilir (varsayilan olarak HICBIR otel
/// verilmez — testler ihtiyaci kadarini acikca verir).
/// </para>
/// </summary>
internal sealed class SettingsAndPersonnelTestHost : ApplicationTestHost
{
    private SettingsAndPersonnelTestHost()
    {
    }

    /// <summary>Marka A'nin Head Office'i — kimlikteki <c>headOfficeId</c> claim'i.</summary>
    public Guid HeadOfficeId { get; private set; }

    /// <summary>A oteli — testlerin varsayilan aktif oteli (marka A).</summary>
    public Guid HotelId { get; private set; }

    /// <summary>B oteli — ayni markada, tenant izolasyonunun "digeri".</summary>
    public Guid OtherHotelId { get; private set; }

    /// <summary>Baska markanin Head Office'i.</summary>
    public Guid OtherBrandHeadOfficeId { get; private set; }

    /// <summary>Baska markanin oteli — hicbir kosulda gorulmemelidir.</summary>
    public Guid OtherBrandHotelId { get; private set; }

    /// <summary>A otelindeki hazir departman ("Rezeption").</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>B otelindeki hazir departman — A'nin kullanicisi icin "yok" sayilmalidir.</summary>
    public Guid OtherHotelDepartmentId { get; private set; }

    /// <summary>Kimlikteki kullanicinin veritabanindaki karsiligi.</summary>
    public Guid UserId { get; private set; }

    /// <summary>A otelindeki hazir oda tipi (oda sayaclarini dogrulamak icin).</summary>
    public Guid RoomTypeId { get; private set; }

    public static async Task<SettingsAndPersonnelTestHost> CreateAsync()
    {
        var host = new SettingsAndPersonnelTestHost();

        try
        {
            await host.InitialiseAsync();

            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>Kimlikteki kullaniciya (veya verilen kullaniciya) bir otel erisimi tanimlar.</summary>
    public async Task GrantHotelAccessAsync(Guid hotelId, Guid? userId = null, bool isDefault = false)
    {
        Database.UserHotelAccesses.Add(new UserHotelAccess
        {
            UserId = userId ?? UserId,
            HotelId = hotelId,
            IsDefault = isDefault
        });

        await SaveAndDetachAsync();
    }

    public async Task<HeadOffice> AddHeadOfficeAsync(string brandName, string defaultCulture = "de")
    {
        var headOffice = new HeadOffice { BrandName = brandName, DefaultCulture = defaultCulture };

        Database.HeadOffices.Add(headOffice);
        await SaveAndDetachAsync();

        return headOffice;
    }

    /// <summary>Otel ekler. <paramref name="isDeleted"/> ile "kapatilmis otel" satiri kurulabilir.</summary>
    public async Task<Hotel> AddHotelAsync(
        Guid headOfficeId,
        string name,
        string city = "Berlin",
        Country country = Country.DE,
        string currency = "EUR",
        string defaultCulture = "de",
        bool isDeleted = false)
    {
        var hotel = new Hotel
        {
            HeadOfficeId = headOfficeId,
            Name = name,
            City = city,
            Country = country,
            Currency = currency,
            DefaultCulture = defaultCulture,
            TaxProfile = new TaxProfile { VatRate = 19m, ReducedVatRate = 7m },
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? Clock.UtcNow : null
        };

        Database.Hotels.Add(hotel);
        await SaveAndDetachAsync();

        return hotel;
    }

    public async Task<Department> AddDepartmentAsync(
        Guid hotelId,
        string name,
        string? description = null)
    {
        var department = new Department
        {
            HotelId = hotelId,
            Name = name,
            Description = description
        };

        Database.Departments.Add(department);
        await SaveAndDetachAsync();

        return department;
    }

    /// <summary>
    /// Calisan ekler. <paramref name="hiredOn"/> verilmezse dondurulmus saate gore bir yil
    /// oncesi kullanilir; <paramref name="isDeleted"/> ile "zaten soft-delete edilmis" satir
    /// kurulabilir.
    /// </summary>
    public async Task<Employee> AddEmployeeAsync(
        Guid hotelId,
        Guid departmentId,
        string firstName,
        string lastName,
        string? staffNumber = null,
        EmploymentType employmentType = EmploymentType.FullTime,
        DateOnly? hiredOn = null,
        DateOnly? terminatedOn = null,
        string? email = null,
        string? phone = null,
        decimal annualLeaveDays = 28m,
        bool isDeleted = false)
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
            HiredOn = hiredOn ?? Clock.Today.AddYears(-1),
            TerminatedOn = terminatedOn,
            Email = email,
            Phone = phone,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? Clock.UtcNow : null
        };

        Database.Employees.Add(employee);
        await SaveAndDetachAsync();

        return employee;
    }

    /// <summary>Oda ekler — otel yanitindaki <c>roomCount</c> alanini dogrulamak icin.</summary>
    public async Task<Room> AddRoomAsync(
        Guid hotelId,
        Guid roomTypeId,
        string number,
        bool isDeleted = false)
    {
        var room = new Room
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Number = number,
            Floor = 1,
            HousekeepingStatus = HousekeepingStatus.Clean,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? Clock.UtcNow : null
        };

        Database.Rooms.Add(room);
        await SaveAndDetachAsync();

        return room;
    }

    /// <summary>Global query filter'i atlayarak ham calisan satirini okur (silinmisler dahil).</summary>
    public Task<Employee?> FindEmployeeIncludingDeletedAsync(Guid id) =>
        Database.Employees.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(employee => employee.Id == id);

    /// <summary>
    /// Global query filter'i atlayarak ham departman satirini okur. Departman
    /// soft-delete EDILEMEZ; bu yardimci "satir gercekten silindi mi" sorusunu yanitlar.
    /// </summary>
    public Task<Department?> FindDepartmentAsync(Guid id) =>
        Database.Departments.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(department => department.Id == id);

    /// <summary>Global query filter'i atlayarak ham otel satirini okur (silinmisler dahil).</summary>
    public Task<Hotel?> FindHotelAsync(Guid id) =>
        // TaxProfile owned type oldugu icin Include gerekmez; otomatik yuklenir.
        Database.Hotels.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(hotel => hotel.Id == id);

    /// <summary>Global query filter'i atlayarak ham Head Office satirini okur.</summary>
    public Task<HeadOffice?> FindHeadOfficeAsync(Guid id) =>
        Database.HeadOffices.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(headOffice => headOffice.Id == id);

    protected override async Task SeedAsync()
    {
        var brandA = await AddHeadOfficeAsync("Marka A Gruppe");
        var brandB = await AddHeadOfficeAsync("Marka B Gruppe");

        HeadOfficeId = brandA.Id;
        OtherBrandHeadOfficeId = brandB.Id;

        HotelId = (await AddHotelAsync(HeadOfficeId, "Hotel A")).Id;
        OtherHotelId = (await AddHotelAsync(HeadOfficeId, "Hotel B")).Id;
        OtherBrandHotelId = (await AddHotelAsync(OtherBrandHeadOfficeId, "Fremdmarke Hotel")).Id;

        DepartmentId = (await AddDepartmentAsync(HotelId, "Rezeption", "Empfang")).Id;
        OtherHotelDepartmentId = (await AddDepartmentAsync(OtherHotelId, "Rezeption B")).Id;

        var roomType = new RoomType
        {
            HotelId = HotelId,
            Code = "DBL",
            Name = "Doppelzimmer",
            BasePrice = 120m,
            Capacity = 2
        };

        Database.RoomTypes.Add(roomType);
        await SaveAndDetachAsync();
        RoomTypeId = roomType.Id;

        // Kimlikteki kullanicinin veritabani karsiligi; UserHotelAccess FK'si bunu gerektirir.
        // Id, EntityBase tarafindan uretilir (proje konvansiyonu) ve kimlige geri yazilir.
        var user = new User
        {
            HeadOfficeId = HeadOfficeId,
            Email = $"handler-test-{Guid.NewGuid():N}@hotelcore.test",
            PasswordHash = "not-a-real-hash",
            FirstName = "Test",
            LastName = "User",
            Culture = "de"
        };

        Database.Users.Add(user);
        await SaveAndDetachAsync();
        UserId = user.Id;

        // Varsayilan baglam: A otelinin kullanicisi (Head Office bypass'i kapali).
        CurrentUser.UserId = UserId;
        CurrentUser.HotelId = HotelId;
        CurrentUser.HeadOfficeId = HeadOfficeId;
    }
}
