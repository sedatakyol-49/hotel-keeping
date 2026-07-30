using System.Globalization;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using HotelCore.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Application.Tests.Support;

/// <summary>
/// Oda yonetimi handler testleri icin kendi kendine yeten mini host.
/// <para>
/// <b>Neden gercek <see cref="AppDbContext"/>:</b> handler'lar <c>IAppDbContext</c> uzerinden
/// calisir ama davranislarinin bir kismi DbContext'te yasar — tenant/soft-delete global query
/// filter'i, <c>Deleted -&gt; Modified</c> soft-delete donusumu, denetim alanlari. Elle yazilmis
/// bir <c>IAppDbContext</c> sahtesi bu davranislari taklit etmek zorunda kalir ve testler
/// gerceklikte dogrulanmayan seyleri dogruluyor gorunurdu.
/// </para>
/// <para>
/// <b>Neden SQLite (Npgsql degil):</b>
/// <list type="bullet">
///   <item>iliskisel bir saglayicidir; LINQ gercekten SQL'e <b>cevrilir</b>. EF Core InMemory
///         saglayicisi her seyi LINQ-to-Objects olarak degerlendirdigi icin "bu sorgu
///         cevrilemiyor" sinifindaki hatalari gizlerdi,</item>
///   <item>kismi unique index (<c>WHERE NOT "IsDeleted"</c>), FK <c>Restrict</c> ve zorunlu
///         kolonlar gercekten olusturulur,</item>
///   <item><c>:memory:</c> veritabani test basina sifirdan kurulur; Docker/daemon gerekmez ve
///         testler paralel kossa bile birbirine dokunmaz (idempotent).</item>
/// </list>
/// <b>SQLite'in taklit ETMEDIGI</b> davranislar bilincli olarak integration katmanina birakildi:
/// PostgreSQL'in <c>ORDER BY length(number), number</c> dogal siralamasi/collation'i,
/// <c>lower(...)</c> davranisi ve benzersizlik ihlalinin (SQLSTATE 23505)
/// <c>ConflictException</c>'a cevrilmesi. Bunlar <c>HotelCore.Api.IntegrationTests</c> icinde
/// gercek PostgreSQL'e karsi dogrulanir — burada sahte bir sonucla yesil gosterilmez.
/// </para>
/// </summary>
internal sealed class RoomModuleTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private RoomModuleTestHost(
        SqliteConnection connection,
        ServiceProvider provider,
        AppDbContext database,
        TestCurrentUser currentUser,
        TestClock clock)
    {
        _connection = connection;
        _provider = provider;
        Database = database;
        CurrentUser = currentUser;
        Clock = clock;
    }

    /// <summary>Aktif kimlik baglami; testler aktif oteli buradan degistirir.</summary>
    public TestCurrentUser CurrentUser { get; }

    /// <summary>Dondurulmus saat (gelecek/gecmis rezervasyon kurallari icin).</summary>
    public TestClock Clock { get; }

    /// <summary>Dogrulama icin dogrudan veritabani erisimi (handler'larla ayni ornek).</summary>
    public AppDbContext Database { get; }

    public IDispatcher Dispatcher => _provider.GetRequiredService<IDispatcher>();

    /// <summary>A oteli — testlerin varsayilan aktif oteli.</summary>
    public Guid HotelId { get; private set; }

    /// <summary>B oteli — tenant izolasyonu ve "baska otele ait kayit" senaryolari icin.</summary>
    public Guid OtherHotelId { get; private set; }

    /// <summary>A otelindeki hazir oda tipi (<c>DBL</c>).</summary>
    public Guid RoomTypeId { get; private set; }

    /// <summary>B otelindeki hazir oda tipi (kod <c>TWN</c>; A otelinde bu kod yoktur).</summary>
    public Guid OtherHotelRoomTypeId { get; private set; }

    /// <summary>Iki otelin bagli oldugu Head Office.</summary>
    public Guid HeadOfficeId { get; private set; }

    public static async Task<RoomModuleTestHost> CreateAsync()
    {
        // Baglanti acik kaldigi surece :memory: veritabani yasar; kapaninca izsiz yok olur.
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        try
        {
            var currentUser = new TestCurrentUser();
            var clock = new TestClock();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            // Kimlik/saat kurucudan verilir: global query filter ifadesi DbContext ORNEGINI
            // yakalar, bu yuzden host boyunca tek bir context ornegi paylasilir.
            var database = new AppDbContext(options, currentUser, clock);

            var services = new ServiceCollection();
            services.AddSingleton(database);
            services.AddSingleton<IAppDbContext>(database);
            services.AddSingleton<ICurrentUser>(currentUser);
            services.AddSingleton<IDateTimeProvider>(clock);
            services.AddLogging();

            // Gercek DI kaydi: dispatcher + boru hatti (logging, validation) + handler'lar +
            // Mapster. Yani testler handler'i tek basina degil, uretimdeki boru hattiyla kosar.
            services.AddApplication();

            var provider = services.BuildServiceProvider();
            var host = new RoomModuleTestHost(connection, provider, database, currentUser, clock);
            await host.SeedAsync();

            return host;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>
    /// Verilen kulturu <see cref="RequestCulture.Current"/>'in okuyacagi sekilde ayarlar ve islem
    /// bitince eski kulturu geri koyar (diger testlere sizmaz).
    /// </summary>
    public static async Task<T> WithCultureAsync<T>(string culture, Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return await action();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>Oda ekler. <paramref name="isDeleted"/> ile "zaten silinmis" satir kurulabilir.</summary>
    public async Task<Room> AddRoomAsync(
        Guid hotelId,
        Guid roomTypeId,
        string number,
        int floor = 1,
        HousekeepingStatus status = HousekeepingStatus.Clean,
        bool isOutOfOrder = false,
        string? note = null,
        bool isDeleted = false)
    {
        var room = new Room
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Number = number,
            Floor = floor,
            HousekeepingStatus = status,
            IsOutOfOrder = isOutOfOrder,
            Note = note,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? Clock.UtcNow : null
        };

        Database.Rooms.Add(room);
        await SaveAndDetachAsync();

        return room;
    }

    public async Task<RoomType> AddRoomTypeAsync(
        Guid hotelId,
        string code,
        string name,
        decimal basePrice = 100m,
        int capacity = 2,
        string? amenities = null)
    {
        var roomType = new RoomType
        {
            HotelId = hotelId,
            Code = code,
            Name = name,
            BasePrice = basePrice,
            Capacity = capacity,
            Amenities = amenities
        };

        Database.RoomTypes.Add(roomType);
        await SaveAndDetachAsync();

        return roomType;
    }

    /// <summary>Odaya rezervasyon (ve misafir) ekler — silme kuralini denemek icin.</summary>
    public async Task<Reservation> AddReservationAsync(
        Guid hotelId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        ReservationStatus status)
    {
        var guest = new Guest
        {
            HotelId = hotelId,
            FirstName = "Test",
            LastName = "Guest"
        };

        var reservation = new Reservation
        {
            HotelId = hotelId,
            RoomId = roomId,
            GuestId = guest.Id,
            ReservationNumber = $"R-{Guid.NewGuid().ToString("N")[..8]}",
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = status
        };

        Database.Guests.Add(guest);
        Database.Reservations.Add(reservation);
        await SaveAndDetachAsync();

        return reservation;
    }

    /// <summary>Oda tipi icin dinamik icerik cevirisi ekler (architecture.md §4.6).</summary>
    public async Task AddTranslationAsync(Guid roomTypeId, string culture, string field, string text)
    {
        Database.Translations.Add(new Translation
        {
            EntityType = TranslationEntityTypes.RoomType,
            EntityId = roomTypeId,
            Culture = culture,
            Field = field,
            Text = text
        });

        await SaveAndDetachAsync();
    }

    /// <summary>Global query filter'i atlayarak ham oda satirini okur (silinmisler dahil).</summary>
    public Task<Room?> FindRoomIncludingDeletedAsync(Guid id) =>
        Database.Rooms.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(room => room.Id == id);

    /// <summary>Global query filter'i atlayarak ham oda tipi satirini okur.</summary>
    public Task<RoomType?> FindRoomTypeIncludingDeletedAsync(Guid id) =>
        Database.RoomTypes.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(type => type.Id == id);

    private async Task SeedAsync()
    {
        await Database.Database.EnsureCreatedAsync();

        var headOffice = new HeadOffice { BrandName = "HotelCore Test", DefaultCulture = "de" };
        var hotelA = NewHotel(headOffice.Id, "Test Hotel A");
        var hotelB = NewHotel(headOffice.Id, "Test Hotel B");

        Database.HeadOffices.Add(headOffice);
        Database.Hotels.Add(hotelA);
        Database.Hotels.Add(hotelB);
        await SaveAndDetachAsync();

        HeadOfficeId = headOffice.Id;
        HotelId = hotelA.Id;
        OtherHotelId = hotelB.Id;

        RoomTypeId = (await AddRoomTypeAsync(HotelId, "DBL", "Doppelzimmer", basePrice: 120m)).Id;
        OtherHotelRoomTypeId = (await AddRoomTypeAsync(OtherHotelId, "TWN", "Zweibettzimmer B")).Id;

        // Varsayilan baglam: A otelinin kullanicisi (Head Office bypass'i kapali).
        CurrentUser.HotelId = HotelId;
        CurrentUser.HeadOfficeId = HeadOfficeId;
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

    /// <summary>
    /// Kaydeder ve change tracker'i temizler: handler'lar veriyi gercekten veritabanindan okumak
    /// zorunda kalir, takip edilen ornek uzerinden "sahte" gecen test olmaz.
    /// </summary>
    private async Task SaveAndDetachAsync()
    {
        await Database.SaveChangesAsync();
        Database.ChangeTracker.Clear();
    }
}
