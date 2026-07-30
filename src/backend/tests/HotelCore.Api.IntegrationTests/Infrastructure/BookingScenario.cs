using System.Globalization;
using System.Net.Http.Headers;
using HotelCore.Api.Services;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Features.Invoices.Common;
using HotelCore.Application.Features.Invoices.Create;
using HotelCore.Application.Features.Invoices.Finalize;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Application.Features.Reservations.Create;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Rezervasyon + Faturalama integration testleri icin <b>test basina</b> izole veri sahnesi.
///
/// <para><b>Idempotentlik:</b> her sahne kendi Head Office'ini, iki otelini (A ve B), oda
/// tiplerini, odalarini ve misafirlerini GUID ekli benzersiz adlarla olusturur;
/// <see cref="DisposeAsync"/> hepsini <b>FK sirasina uygun</b> sekilde fiziksel olarak siler.
/// Fatura numarasi sekansi otel bazinda oldugu icin her sahne <c>{yil}-000001</c>'den baslar —
/// testler ayni veritabaninda tekrar tekrar ve paralel kosabilir, artik satir birakmaz.</para>
///
/// <para><b>Storno zinciri temizligi:</b> <c>Invoices</c> tablosunda iki kendine referans veren FK
/// vardir (<c>CancelledByInvoiceId</c>, <c>CancelsInvoiceId</c>). Tek bir DELETE ifadesi satirlari
/// silerken bu referanslari ihlal edebilir, bu yuzden silmeden ONCE her iki kolon NULL'lanir.</para>
///
/// <para><b>Token uretimi:</b> JWT'ler uygulamanin kendi <see cref="IJwtTokenService"/> servisiyle
/// imzalanir; claim semasi (<c>sub</c>, <c>perm</c>, <c>hotel</c>, <c>allHotels</c>) uretimdekiyle
/// birebir aynidir.</para>
/// </summary>
internal sealed class BookingScenario : IAsyncDisposable
{
    /// <summary>Kurtaxe: kisi basi gecelik tutar (12,00 / 24,00 senaryosunun tabani).</summary>
    public const decimal CityTaxPerPersonNight = 3.00m;

    /// <summary>A otelindeki oda tipinin liste fiyati (fiyat plani yoksa kullanilir).</summary>
    public const decimal BasePrice = 120m;

    private readonly PostgresFixture _fixture;
    private readonly List<HttpClient> _clients = [];
    private readonly List<ApplicationGraph> _graphs = [];

    private BookingScenario(PostgresFixture fixture, Guid headOfficeId, Guid hotelAId, Guid hotelBId)
    {
        _fixture = fixture;
        HeadOfficeId = headOfficeId;
        HotelAId = hotelAId;
        HotelBId = hotelBId;
    }

    public Guid HeadOfficeId { get; }

    /// <summary>A oteli — testlerin varsayilan oteli.</summary>
    public Guid HotelAId { get; }

    /// <summary>B oteli — tenant izolasyonunun "digeri".</summary>
    public Guid HotelBId { get; }

    /// <summary>A otelindeki oda tipi (kapasite 4 — 2 yetiskin + 2 cocuk senaryosu icin).</summary>
    public Guid RoomTypeAId { get; private set; }

    public Guid RoomTypeBId { get; private set; }

    /// <summary>A otelindeki oda.</summary>
    public Guid RoomAId { get; private set; }

    /// <summary>A otelindeki ikinci oda.</summary>
    public Guid SecondRoomAId { get; private set; }

    /// <summary>A otelindeki servis disi oda.</summary>
    public Guid OutOfOrderRoomAId { get; private set; }

    public Guid RoomBId { get; private set; }

    public Guid GuestAId { get; private set; }

    public Guid GuestBId { get; private set; }

    /// <summary>Dispatcher seviyesindeki testlerin varsayilan uygulama grafigi (A oteli baglami).</summary>
    public ApplicationGraph Host { get; private set; } = null!;

    public FrozenClock Clock => Host.Clock;

    public DateOnly Today => Host.Clock.Today;

    public int Year => Host.Clock.Year;

    public static async Task<BookingScenario> StartAsync(PostgresFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await fixture.EnsureMigratedAsync();

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var headOffice = new HeadOffice { BrandName = $"IT Booking {suffix}", DefaultCulture = "de" };
        var hotelA = NewHotel(headOffice.Id, $"IT Booking A {suffix}");
        var hotelB = NewHotel(headOffice.Id, $"IT Booking B {suffix}");

        var scenario = new BookingScenario(fixture, headOffice.Id, hotelA.Id, hotelB.Id);

        var roomTypeA = new RoomType
        {
            HotelId = hotelA.Id,
            Code = "DBL",
            Name = "Doppelzimmer",
            BasePrice = BasePrice,
            Capacity = 4
        };

        var roomTypeB = new RoomType
        {
            HotelId = hotelB.Id,
            Code = "TWN",
            Name = "Zweibettzimmer B",
            BasePrice = 100m,
            Capacity = 2
        };

        await using (var database = fixture.CreateDbContext())
        {
            database.HeadOffices.Add(headOffice);
            database.Hotels.Add(hotelA);
            database.Hotels.Add(hotelB);
            database.RoomTypes.Add(roomTypeA);
            database.RoomTypes.Add(roomTypeB);
            await database.SaveChangesAsync();
        }

        scenario.RoomTypeAId = roomTypeA.Id;
        scenario.RoomTypeBId = roomTypeB.Id;

        scenario.RoomAId = await scenario.AddRoomAsync(hotelA.Id, roomTypeA.Id, "101");
        scenario.SecondRoomAId = await scenario.AddRoomAsync(hotelA.Id, roomTypeA.Id, "102");
        scenario.OutOfOrderRoomAId =
            await scenario.AddRoomAsync(hotelA.Id, roomTypeA.Id, "199", isOutOfOrder: true);
        scenario.RoomBId = await scenario.AddRoomAsync(hotelB.Id, roomTypeB.Id, "B01");

        scenario.GuestAId = await scenario.AddGuestAsync(hotelA.Id, "Anna", "Muster");
        scenario.GuestBId = await scenario.AddGuestAsync(hotelB.Id, "Bert", "Fremd");

        scenario.Host = scenario.CreateApplicationGraph();

        return scenario;
    }

    // ---------------------------------------------------------------------------------------
    // Uygulama grafigi (dispatcher seviyesi)
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Bu sahneye bagli yeni bir uygulama grafigi uretir. Ikinci bir grafik istemek
    /// <b>eszamanlilik</b> senaryolari icindir: ayri change tracker olmadan optimistic
    /// concurrency token'i (<c>HotelInvoiceCounter.Version</c>) hicbir zaman tetiklenmez.
    /// </summary>
    public ApplicationGraph CreateApplicationGraph(Guid? activeHotelId = null, DateTimeOffset? now = null)
    {
        var graph = new ApplicationGraph(
            _fixture.ConnectionString,
            new ScenarioIdentity
            {
                HotelId = activeHotelId ?? HotelAId,
                HeadOfficeId = HeadOfficeId
            },
            new FrozenClock { UtcNow = now ?? Host?.Clock.UtcNow ?? DateTimeOffset.UtcNow });

        _graphs.Add(graph);

        return graph;
    }

    // ---------------------------------------------------------------------------------------
    // Veri kurulumu
    // ---------------------------------------------------------------------------------------

    public async Task<Guid> AddRoomAsync(Guid hotelId, Guid roomTypeId, string number, bool isOutOfOrder = false)
    {
        var room = new Room
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Number = number,
            Floor = 1,
            HousekeepingStatus = isOutOfOrder ? HousekeepingStatus.OutOfOrder : HousekeepingStatus.Clean,
            IsOutOfOrder = isOutOfOrder
        };

        await using var database = _fixture.CreateDbContext();
        database.Rooms.Add(room);
        await database.SaveChangesAsync();

        return room.Id;
    }

    public async Task<Guid> AddGuestAsync(Guid hotelId, string firstName, string lastName)
    {
        var guest = new Guest { HotelId = hotelId, FirstName = firstName, LastName = lastName };

        await using var database = _fixture.CreateDbContext();
        database.Guests.Add(guest);
        await database.SaveChangesAsync();

        return guest.Id;
    }

    /// <summary>Fiyat planini <b>dogrudan</b> veritabanina yazar (handler on kontrolunu atlar).</summary>
    public async Task<Guid> AddRatePlanDirectlyAsync(
        Guid hotelId,
        Guid roomTypeId,
        string name,
        decimal price,
        DateOnly validFrom,
        DateOnly validTo,
        ReservationChannel? channel = null,
        bool isActive = true)
    {
        var plan = new RatePlan
        {
            HotelId = hotelId,
            RoomTypeId = roomTypeId,
            Name = name,
            Price = price,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Channel = channel,
            IsActive = isActive
        };

        await using var database = _fixture.CreateDbContext();
        database.RatePlans.Add(plan);
        await database.SaveChangesAsync();

        return plan.Id;
    }

    /// <summary>Otelin Kurtaxe ayarlarini degistirir (cocuk muafiyeti senaryolari).</summary>
    public async Task ConfigureCityTaxAsync(
        bool enabled = true,
        decimal perPersonNight = CityTaxPerPersonNight,
        bool exemptChildren = false,
        int? childAgeLimit = 18)
    {
        await using var database = _fixture.CreateDbContext();
        var hotel = await database.Hotels.IgnoreQueryFilters().FirstAsync(candidate => candidate.Id == HotelAId);

        hotel.TaxProfile.CityTaxEnabled = enabled;
        hotel.TaxProfile.CityTaxPerPersonNight = perPersonNight;
        hotel.TaxProfile.CityTaxExemptChildren = exemptChildren;
        hotel.TaxProfile.CityTaxChildAgeLimit = childAgeLimit;

        await database.SaveChangesAsync();
    }

    // ---------------------------------------------------------------------------------------
    // Kisayollar (varsayilan grafik uzerinden)
    // ---------------------------------------------------------------------------------------

    public Task<ReservationResponse> CreateReservationAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? roomId = null,
        int adults = 2,
        int children = 0,
        ReservationChannel channel = ReservationChannel.Direct) =>
        Host.Dispatcher.Send(new CreateReservationRequest
        {
            RoomId = roomId ?? RoomAId,
            GuestId = GuestAId,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Adults = adults,
            Children = children,
            Channel = channel
        });

    public Task<InvoiceDetailResponse> CreateManualInvoiceAsync(params InvoiceLineInput[] lines) =>
        Host.Dispatcher.Send(new CreateInvoiceRequest
        {
            GuestId = GuestAId,
            LineItems = lines.Length > 0 ? lines : [Line(InvoiceLineType.Extra, "Minibar", 1m, 10m)]
        });

    public Task<InvoiceDetailResponse> CreateReservationInvoiceAsync(Guid reservationId) =>
        Host.Dispatcher.Send(new CreateInvoiceRequest { ReservationId = reservationId });

    public Task<InvoiceDetailResponse> FinalizeInvoiceAsync(Guid invoiceId) =>
        Host.Dispatcher.Send(new FinalizeInvoiceRequest(invoiceId));

    public async Task<InvoiceDetailResponse> CreateFinalizedInvoiceAsync(params InvoiceLineInput[] lines)
    {
        var draft = await CreateManualInvoiceAsync(lines);

        return await FinalizeInvoiceAsync(draft.Id);
    }

    public static InvoiceLineInput Line(
        InvoiceLineType type,
        string description,
        decimal quantity,
        decimal unitPrice) =>
        new()
        {
            Type = type,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice
        };

    /// <summary>
    /// Beklenen fatura numarasi. Sahne kendi otelini olusturdugu icin sekans daima 1'den baslar;
    /// yil gercek takvimden gelir (sabit bir yil yazmak testi yil donumunde kirardi).
    /// </summary>
    public string InvoiceNumber(int sequence) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Year:0000}-{sequence:000000}");

    // ---------------------------------------------------------------------------------------
    // HTTP istemcileri
    // ---------------------------------------------------------------------------------------

    /// <summary>Verilen izin kumesiyle imzalanmis token tasiyan istemci uretir.</summary>
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

    // ---------------------------------------------------------------------------------------
    // Ham okuma (tenant filtresi atlanarak)
    // ---------------------------------------------------------------------------------------

    public async Task<Invoice?> FindInvoiceAsync(Guid id)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.Invoices.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(invoice => invoice.Id == id);
    }

    public async Task<Room?> FindRoomAsync(Guid id)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.Rooms.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(room => room.Id == id);
    }

    public async Task<HotelInvoiceCounter?> FindInvoiceCounterAsync(Guid? hotelId = null)
    {
        var target = hotelId ?? HotelAId;

        await using var database = _fixture.CreateDbContext();

        return await database.HotelInvoiceCounters.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(counter => counter.HotelId == target && counter.Year == Year);
    }

    /// <summary>Otelin verilmis tum fatura numaralari (sekans denetimi icin sirali).</summary>
    public async Task<IReadOnlyList<string>> ListInvoiceNumbersAsync(Guid? hotelId = null)
    {
        var target = hotelId ?? HotelAId;

        await using var database = _fixture.CreateDbContext();

        return await database.Invoices.IgnoreQueryFilters().AsNoTracking()
            .Where(invoice => invoice.HotelId == target && invoice.InvoiceNumber != "")
            .OrderBy(invoice => invoice.InvoiceNumber)
            .Select(invoice => invoice.InvoiceNumber)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<InvoiceAuditEntry>> ListAuditEntriesAsync(Guid invoiceId)
    {
        await using var database = _fixture.CreateDbContext();

        return await database.InvoiceAuditEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(entry => entry.InvoiceId == invoiceId)
            .ToListAsync();
    }

    /// <summary>Sahnenin urettigi tum satirlari fiziksel olarak siler (FK sirasina uygun).</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();

        foreach (var graph in _graphs)
        {
            await graph.DisposeAsync();
        }

        _graphs.Clear();

        if (string.IsNullOrEmpty(_fixture.ConnectionString))
        {
            return;
        }

        await using var database = _fixture.CreateDbContext();
        Guid[] hotelIds = [HotelAId, HotelBId];

        // Storno cifti kendine referans veren iki FK uretir; DELETE'ten once koparilir.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Invoices"
             SET "CancelledByInvoiceId" = NULL, "CancelsInvoiceId" = NULL
             WHERE "HotelId" = ANY({hotelIds})
             """);

        await database.InvoiceAuditEntries.IgnoreQueryFilters()
            .Where(entry => hotelIds.Contains(entry.HotelId)).ExecuteDeleteAsync();
        await database.Payments.IgnoreQueryFilters()
            .Where(payment => hotelIds.Contains(payment.HotelId)).ExecuteDeleteAsync();
        await database.InvoiceLineItems.IgnoreQueryFilters()
            .Where(line => hotelIds.Contains(line.HotelId)).ExecuteDeleteAsync();
        await database.Invoices.IgnoreQueryFilters()
            .Where(invoice => hotelIds.Contains(invoice.HotelId)).ExecuteDeleteAsync();
        await database.HotelInvoiceCounters.IgnoreQueryFilters()
            .Where(counter => hotelIds.Contains(counter.HotelId)).ExecuteDeleteAsync();
        await database.Folios.IgnoreQueryFilters()
            .Where(folio => hotelIds.Contains(folio.HotelId)).ExecuteDeleteAsync();
        await database.Reservations.IgnoreQueryFilters()
            .Where(reservation => hotelIds.Contains(reservation.HotelId)).ExecuteDeleteAsync();
        await database.RatePlans.IgnoreQueryFilters()
            .Where(plan => hotelIds.Contains(plan.HotelId)).ExecuteDeleteAsync();
        await database.Rooms.IgnoreQueryFilters()
            .Where(room => hotelIds.Contains(room.HotelId)).ExecuteDeleteAsync();
        await database.Guests.IgnoreQueryFilters()
            .Where(guest => hotelIds.Contains(guest.HotelId)).ExecuteDeleteAsync();
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
        TaxProfile = new TaxProfile
        {
            VatRate = 19m,
            ReducedVatRate = 7m,
            CityTaxEnabled = true,
            CityTaxPerPersonNight = CityTaxPerPersonNight,
            CityTaxExemptChildren = false,
            CityTaxChildAgeLimit = 18
        }
    };
}
