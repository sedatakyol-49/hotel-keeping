using System.Net.Http.Headers;
using HotelCore.Api.Services;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Oda yonetimi integration testleri icin <b>test basina</b> izole veri sahnesi.
/// <para>
/// <b>Idempotentlik:</b> her sahne kendi Head Office'ini, iki otelini (A ve B) ve oda tiplerini
/// GUID ekli benzersiz adlarla olusturur; <see cref="DisposeAsync"/> icinde hepsini FK sirasina
/// uygun sekilde <b>fiziksel olarak</b> siler. Boylece testler ayni veritabaninda tekrar tekrar
/// kosabilir, sabit GUID/isim cakismasi olmaz ve baska testlerin verisine dokunmaz. Her sahnenin
/// kendi oteli oldugu icin "9/10/100" gibi oda numaralari testler arasinda catismaz.
/// </para>
/// <para>
/// <b>Token uretimi:</b> JWT'ler uygulamanin kendi <see cref="IJwtTokenService"/> servisiyle,
/// test konfigurasyonundaki <c>Jwt:Secret</c> ile imzalanir (bkz. <see cref="HotelCoreApiFactory"/>).
/// Claim semasi (<c>sub</c>, <c>perm</c>, <c>hotel</c>, <c>allHotels</c>) boylece uretimdekiyle
/// birebir aynidir — testte elle claim kurmak semadan sapma riski tasirdi.
/// </para>
/// </summary>
internal sealed class RoomModuleScenario : IAsyncDisposable
{
    private readonly PostgresFixture _fixture;
    private readonly List<HttpClient> _clients = [];

    private RoomModuleScenario(PostgresFixture fixture, Guid headOfficeId, Guid hotelAId, Guid hotelBId)
    {
        _fixture = fixture;
        HeadOfficeId = headOfficeId;
        HotelAId = hotelAId;
        HotelBId = hotelBId;
    }

    /// <summary>Bu sahnenin Head Office'i (iki otelin de sahibi).</summary>
    public Guid HeadOfficeId { get; }

    /// <summary>A oteli — testlerin varsayilan oteli.</summary>
    public Guid HotelAId { get; }

    /// <summary>B oteli — tenant izolasyonunun "digeri".</summary>
    public Guid HotelBId { get; }

    /// <summary>A otelindeki oda tipi.</summary>
    public Guid RoomTypeAId { get; private set; }

    /// <summary>B otelindeki oda tipi.</summary>
    public Guid RoomTypeBId { get; private set; }

    /// <summary>A otelindeki oda tipinin liste fiyati — pano yanitinda GORUNMEMELIDIR.</summary>
    public decimal RoomTypeABasePrice { get; } = 199.99m;

    public static async Task<RoomModuleScenario> StartAsync(PostgresFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await fixture.EnsureMigratedAsync();

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var headOffice = new HeadOffice { BrandName = $"IT Brand {suffix}", DefaultCulture = "de" };
        var hotelA = NewHotel(headOffice.Id, $"IT Hotel A {suffix}");
        var hotelB = NewHotel(headOffice.Id, $"IT Hotel B {suffix}");

        var scenario = new RoomModuleScenario(fixture, headOffice.Id, hotelA.Id, hotelB.Id);

        await using var database = fixture.CreateDbContext();
        database.HeadOffices.Add(headOffice);
        database.Hotels.Add(hotelA);
        database.Hotels.Add(hotelB);

        var roomTypeA = new RoomType
        {
            HotelId = hotelA.Id,
            Code = "DBL",
            Name = "Doppelzimmer",
            BasePrice = scenario.RoomTypeABasePrice,
            Capacity = 2,
            Amenities = "wifi,minibar"
        };

        var roomTypeB = new RoomType
        {
            HotelId = hotelB.Id,
            Code = "DBL",
            Name = "Doppelzimmer B",
            BasePrice = 149.50m,
            Capacity = 2
        };

        database.RoomTypes.Add(roomTypeA);
        database.RoomTypes.Add(roomTypeB);
        await database.SaveChangesAsync();

        scenario.RoomTypeAId = roomTypeA.Id;
        scenario.RoomTypeBId = roomTypeB.Id;

        return scenario;
    }

    /// <summary>API'yi hic kullanmadan dogrudan veritabanina oda ekler (kurulum/izolasyon icin).</summary>
    public async Task<Guid> AddRoomAsync(
        Guid hotelId,
        Guid roomTypeId,
        string number,
        int floor = 1,
        HousekeepingStatus status = HousekeepingStatus.Clean,
        string? note = null)
    {
        var room = new Room
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Number = number,
            Floor = floor,
            HousekeepingStatus = status,
            IsOutOfOrder = status is HousekeepingStatus.OutOfOrder,
            Note = note
        };

        await using var database = _fixture.CreateDbContext();
        database.Rooms.Add(room);
        await database.SaveChangesAsync();

        return room.Id;
    }

    /// <summary>
    /// Verilen izin kumesiyle imzalanmis token tasiyan istemci uretir.
    /// </summary>
    /// <param name="permissions">
    /// <c>perm</c> claim'leri. Policy adi = izin anahtari oldugu icin bir izni LISTEDEN
    /// CIKARMAK ilgili ucta 403 uretmenin tek dogru yoludur.
    /// </param>
    /// <param name="hotelIds">
    /// <c>hotel</c> claim'leri (erisilebilir oteller). Sira anlamlidir: ilk otel varsayilan
    /// aktif oteldir. Verilmezse yalnizca A oteli.
    /// </param>
    /// <param name="canAccessAllHotels">Head Office bypass'i (<c>allHotels</c>).</param>
    /// <param name="activeHotelId">Verilirse <c>X-Hotel-Id</c> header'i olarak gonderilir.</param>
    public HttpClient CreateClient(
        IReadOnlyList<string> permissions,
        IReadOnlyList<Guid>? hotelIds = null,
        bool canAccessAllHotels = false,
        Guid? activeHotelId = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var descriptor = new AccessTokenDescriptor(
            UserId: Guid.NewGuid(),
            Email: $"it-{Guid.NewGuid():N}@hotelcore.test",
            HeadOfficeId: HeadOfficeId,
            Culture: "de",
            Permissions: permissions,
            HotelIds: hotelIds ?? [HotelAId],
            CanAccessAllHotels: canAccessAllHotels);

        using var scope = _fixture.Api.Services.CreateScope();
        var accessToken = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .CreateAccessToken(descriptor);

        var client = _fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Value);

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

    /// <summary>Global query filter'i atlayarak ham oda satirini okur (silinmisler dahil).</summary>
    public async Task<Room?> FindRoomIncludingDeletedAsync(Guid roomId)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.Rooms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(room => room.Id == roomId);
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
        Guid[] hotelIds = [HotelAId, HotelBId];

        var roomTypeIds = await database.RoomTypes
            .IgnoreQueryFilters()
            .Where(roomType => hotelIds.Contains(roomType.HotelId))
            .Select(roomType => roomType.Id)
            .ToArrayAsync();

        await database.Reservations.IgnoreQueryFilters()
            .Where(reservation => hotelIds.Contains(reservation.HotelId)).ExecuteDeleteAsync();
        await database.Rooms.IgnoreQueryFilters()
            .Where(room => hotelIds.Contains(room.HotelId)).ExecuteDeleteAsync();
        await database.Guests.IgnoreQueryFilters()
            .Where(guest => hotelIds.Contains(guest.HotelId)).ExecuteDeleteAsync();
        await database.Translations
            .Where(translation => roomTypeIds.Contains(translation.EntityId)).ExecuteDeleteAsync();
        await database.RoomTypes.IgnoreQueryFilters()
            .Where(roomType => hotelIds.Contains(roomType.HotelId)).ExecuteDeleteAsync();
        await database.Hotels.IgnoreQueryFilters()
            .Where(hotel => hotelIds.Contains(hotel.Id)).ExecuteDeleteAsync();
        await database.HeadOffices
            .Where(headOffice => headOffice.Id == HeadOfficeId).ExecuteDeleteAsync();
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
