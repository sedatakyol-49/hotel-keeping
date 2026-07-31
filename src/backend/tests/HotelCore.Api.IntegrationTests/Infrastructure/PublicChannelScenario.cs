using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using HotelCore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HotelCore.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Misafire acik kanal integration testleri icin <b>test basina</b> izole veri sahnesi:
/// iki otel (A ve B), her birinin kendi slug'i, oda tipleri, odalari, hukuki belgeleri ve
/// web'e uygulanabilir fiyat plani.
///
/// <para><b>Neden HTTP uzerinden:</b> public kanalin davranisinin buyuk kismi <b>middleware'de</b>
/// yasar — tenant kapsaminin yoldan cozulmesi, hiz siniri, kart tuzak teli, Content-Language ve
/// cache basliklari. Dispatcher seviyesinde kosulan bir test bunlarin hicbirini gormezdi.</para>
///
/// <para><b>Slug'lar benzersizdir</b> (<c>it-a-{guid}</c>): slug canli satirlar arasinda GLOBAL
/// benzersizdir ve testler ayni veritabaninda tekrar tekrar kosar. Ayrica hiz siniri bolumleme
/// anahtari <c>(slug, IP)</c> oldugu icin her sahne kendi kotasina sahiptir — bir testin
/// urettigi trafik digerini 429'a dusurmez.</para>
/// </summary>
internal sealed class PublicChannelScenario : IAsyncDisposable
{
    /// <summary>Kurtaxe: kisi basi gecelik tutar.</summary>
    public const decimal CityTaxPerPersonNight = 3.00m;

    /// <summary>A otelindeki oda tipinin liste fiyati.</summary>
    public const decimal BasePrice = 120m;

    /// <summary>Web'e uygulanabilir plan fiyati (Channel = null → "tum kanallar").</summary>
    public const decimal WebRatePrice = 139m;

    /// <summary>Demo hukuki metinlerin yayin versiyonu.</summary>
    public const string LegalVersion = "2026-07-01";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PostgresFixture _fixture;
    private readonly List<HttpClient> _clients = [];

    private PublicChannelScenario(PostgresFixture fixture, Guid headOfficeId, Guid hotelAId, Guid hotelBId)
    {
        _fixture = fixture;
        HeadOfficeId = headOfficeId;
        HotelAId = hotelAId;
        HotelBId = hotelBId;
    }

    public Guid HeadOfficeId { get; }

    public Guid HotelAId { get; }

    /// <summary>B oteli — tenant izolasyonunun "digeri".</summary>
    public Guid HotelBId { get; }

    public string BrandSlug { get; private set; } = string.Empty;

    public string SlugA { get; private set; } = string.Empty;

    public string SlugB { get; private set; } = string.Empty;

    public Guid RoomTypeAId { get; private set; }

    public Guid RoomTypeBId { get; private set; }

    /// <summary>A otelindeki oda kimlikleri (sirali: 101, 102).</summary>
    public IReadOnlyList<Guid> RoomAIds { get; private set; } = [];

    /// <summary>A otelinin oda tipi kodu.</summary>
    public const string RoomTypeCodeA = "DBL";

    /// <summary>B otelinin oda tipi kodu — A'da YOKTUR (izolasyon testi icin).</summary>
    public const string RoomTypeCodeB = "TWNB";

    /// <summary>Anonim HTTP istemcisi — public uclar kimlik istemez.</summary>
    public HttpClient Client { get; private set; } = null!;

    public static async Task<PublicChannelScenario> StartAsync(
        PostgresFixture fixture,
        int roomCountA = 2,
        bool channelEnabledB = true)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await fixture.EnsureMigratedAsync();

        var suffix = Guid.NewGuid().ToString("N")[..12];
        var headOffice = new HeadOffice
        {
            BrandName = $"IT Public {suffix}",
            DefaultCulture = "de",
            PublicSlug = $"it-brand-{suffix}"
        };

        var hotelA = NewHotel(headOffice.Id, $"IT Public A {suffix}", $"it-a-{suffix}", enabled: true);
        var hotelB = NewHotel(headOffice.Id, $"IT Public B {suffix}", $"it-b-{suffix}", channelEnabledB);

        var scenario = new PublicChannelScenario(fixture, headOffice.Id, hotelA.Id, hotelB.Id)
        {
            BrandSlug = headOffice.PublicSlug!,
            SlugA = hotelA.PublicSlug!,
            SlugB = hotelB.PublicSlug!
        };

        var roomTypeA = new RoomType
        {
            HotelId = hotelA.Id,
            Code = RoomTypeCodeA,
            Name = "Doppelzimmer",
            Description = "Grosszuegiges Doppelzimmer mit Kingsize-Bett.",
            BasePrice = BasePrice,
            Capacity = 4,
            Amenities = "wifi,minibar"
        };

        var roomTypeB = new RoomType
        {
            HotelId = hotelB.Id,
            Code = RoomTypeCodeB,
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

            // Web'e uygulanabilir plan: fiyat secimi kanali BIREBIR karsilastirir, bu yuzden
            // Channel = null ("tum kanallar") plan olmadan web fiyati BasePrice'a duserdi.
            database.RatePlans.Add(new RatePlan
            {
                HotelId = hotelA.Id,
                RoomTypeId = roomTypeA.Id,
                Name = "IT Web Rate",
                Price = WebRatePrice,
                ValidFrom = new DateOnly(2020, 1, 1),
                ValidTo = new DateOnly(2099, 12, 31),
                Channel = null,
                IsActive = true
            });

            foreach (var document in LegalDocuments(hotelA.Id))
            {
                database.HotelLegalDocuments.Add(document);
            }

            database.HotelImages.Add(new HotelImage
            {
                HotelId = hotelA.Id,
                Url = "/assets/it/hero.jpg",
                SortOrder = 0,
                AltText = "Fassade",
                Width = 1600,
                Height = 900
            });

            await database.SaveChangesAsync();
        }

        scenario.RoomTypeAId = roomTypeA.Id;
        scenario.RoomTypeBId = roomTypeB.Id;

        var rooms = new List<Guid>();
        for (var index = 0; index < roomCountA; index++)
        {
            rooms.Add(await scenario.AddRoomAsync(
                hotelA.Id,
                roomTypeA.Id,
                string.Create(CultureInfo.InvariantCulture, $"{101 + index}")));
        }

        scenario.RoomAIds = rooms;
        await scenario.AddRoomAsync(hotelB.Id, roomTypeB.Id, "B01");

        scenario.Client = fixture.Api.CreateClient();
        scenario._clients.Add(scenario.Client);

        return scenario;
    }

    // -------------------------------------------------------------------------------------------
    // Public uc kisayollari
    // -------------------------------------------------------------------------------------------

    /// <summary>Public uc yolu (varsayilan A oteli).</summary>
    public string Path(string relative, string? slug = null) =>
        $"/api/v1/public/hotels/{slug ?? SlugA}{relative}";

    public Task<HttpResponseMessage> GetAsync(string relative, string? slug = null) =>
        Client.GetAsync(new Uri(Path(relative, slug), UriKind.Relative));

    /// <summary>
    /// Ham JSON gonderir. <b>Neden nesne degil metin:</b> kart tuzak teli gibi testler
    /// sozlesmede <i>bulunmayan</i> alanlar gondermek zorundadir; tipli bir istek nesnesi
    /// bunu ifade edemezdi.
    /// </summary>
    public Task<HttpResponseMessage> PostRawAsync(string relative, string json, string? slug = null) =>
        Client.PostAsync(
            new Uri(Path(relative, slug), UriKind.Relative),
            new StringContent(json, Encoding.UTF8, "application/json"));

    public Task<HttpResponseMessage> PostAsync<T>(string relative, T body, string? slug = null) =>
        Client.PostAsJsonAsync(new Uri(Path(relative, slug), UriKind.Relative), body, Json);

    /// <summary>Hold olusturur ve ham JSON dokumanini dondurur.</summary>
    public async Task<(HttpResponseMessage Response, JsonDocument? Body)> CreateHoldAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        int adults = 2,
        int children = 0,
        string roomTypeCode = RoomTypeCodeA,
        string? slug = null)
    {
        var json = JsonSerializer.Serialize(
            new { roomTypeCode, checkIn, checkOut, adults, children },
            Json);

        var response = await PostRawAsync("/holds", json, slug);
        var body = await ReadJsonAsync(response);

        return (response, body);
    }

    /// <summary>Rezervasyon istegi govdesini sozlesmedeki tam sekliyle uretir.</summary>
    public static string BookingJson(
        string holdToken,
        string summaryHash,
        string email = "juergen.mueller@example.de",
        string? termsVersion = LegalVersion)
    {
        var payload = new
        {
            holdToken,
            checkout = new { summaryHash, orderButtonLabel = "zahlungspflichtig buchen" },
            guest = new
            {
                firstName = "Jürgen",
                lastName = "Müller",
                email,
                phone = (string?)null,
                culture = "de",
                countryOfResidence = "DE"
            },
            invoiceAddress = (object?)null,
            stay = new { estimatedArrivalLocalTime = "18:00", guestNote = (string?)null },
            payment = new { method = "PayAtProperty", guarantee = (string?)null },
            consents = new
            {
                termsAccepted = true,
                termsVersion,
                privacyNoticeAcknowledged = true,
                privacyNoticeVersion = LegalVersion,
                withdrawalNoticeAcknowledged = true,
                withdrawalNoticeVersion = LegalVersion,
                bookerIsAdult = true,
                marketingOptIn = false
            },
            challengeToken = (string?)null
        };

        return JsonSerializer.Serialize(payload, Json);
    }

    public static async Task<JsonDocument?> ReadJsonAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var raw = await response.Content.ReadAsStringAsync();

        return string.IsNullOrWhiteSpace(raw) ? null : JsonDocument.Parse(raw);
    }

    /// <summary>Yanit govdesinin ham metni (yasak alan taramasi icin).</summary>
    public static Task<string> ReadRawAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Content.ReadAsStringAsync();
    }

    // -------------------------------------------------------------------------------------------
    // Veri kurulumu / okuma
    // -------------------------------------------------------------------------------------------

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

    /// <summary>Referanstan rezervasyon kimligini cozer (fatura uretmek icin).</summary>
    public async Task<Guid> FindReservationIdAsync(string bookingReference)
    {
        var normalized = bookingReference.Replace("-", string.Empty, StringComparison.Ordinal);

        await using var database = _fixture.CreateDbContext();

        return await database.PublicBookings.IgnoreQueryFilters().AsNoTracking()
            .Where(booking => booking.BookingReference == normalized)
            .Select(booking => booking.ReservationId)
            .FirstAsync();
    }

    public async Task<int> CountActiveHoldsAsync()
    {
        await using var database = _fixture.CreateDbContext();

        return await database.BookingHolds.IgnoreQueryFilters()
            .CountAsync(hold => hold.HotelId == HotelAId && hold.ConsumedAt == null);
    }

    /// <summary>
    /// Admin tarafi icin token tasiyan istemci. Public kanalin admin eklentileri
    /// (<c>publicReference</c>, <c>/public-booking</c>, ayar bloklari) YENI izin anahtari
    /// GEREKTIRMEZ; mevcut izinlerle calisir.
    /// </summary>
    public HttpClient CreateAdminClient(params string[] permissions) =>
        CreateAdminClient(canAccessAllHotels: false, permissions);

    /// <summary>
    /// <paramref name="canAccessAllHotels"/> Head Office kapsamı içindir: otel <b>okuma</b> yolu
    /// (<c>HotelReader</c>) erişimi <c>UserHotelAccess</c> tablosundan doğrular, JWT claim'inden
    /// değil — sahne o satırları kurmadığı için ayar testleri konsolide kapsam kullanır.
    /// </summary>
    public HttpClient CreateAdminClient(bool canAccessAllHotels, params string[] permissions)
    {
        var descriptor = new AccessTokenDescriptor(
            UserId: Guid.NewGuid(),
            Email: $"it-{Guid.NewGuid():N}@hotelcore.test",
            HeadOfficeId: HeadOfficeId,
            Culture: "de",
            Permissions: permissions,
            HotelIds: [HotelAId],
            CanAccessAllHotels: canAccessAllHotels);

        using var scope = _fixture.Api.Services.CreateScope();
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtTokenService>()
            .CreateAccessToken(descriptor);

        var client = _fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        _clients.Add(client);

        return client;
    }

    /// <summary>Dispatcher seviyesindeki islemler icin (fatura uretimi) uygulama grafigi.</summary>
    public ApplicationGraph CreateApplicationGraph(Guid? hotelId = null) =>
        new(
            _fixture.ConnectionString,
            new ScenarioIdentity { HotelId = hotelId ?? HotelAId, HeadOfficeId = HeadOfficeId },
            new FrozenClock());

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

        // Public kayitlar rezervasyondan ONCE silinir (FK Restrict).
        await database.PublicBookings.IgnoreQueryFilters()
            .Where(booking => hotelIds.Contains(booking.HotelId)).ExecuteDeleteAsync();
        await database.BookingHolds.IgnoreQueryFilters()
            .Where(hold => hotelIds.Contains(hold.HotelId)).ExecuteDeleteAsync();
        await database.Reservations.IgnoreQueryFilters()
            .Where(reservation => hotelIds.Contains(reservation.HotelId)).ExecuteDeleteAsync();
        await database.RatePlans.IgnoreQueryFilters()
            .Where(plan => hotelIds.Contains(plan.HotelId)).ExecuteDeleteAsync();
        await database.Rooms.IgnoreQueryFilters()
            .Where(room => hotelIds.Contains(room.HotelId)).ExecuteDeleteAsync();
        await database.Guests.IgnoreQueryFilters()
            .Where(guest => hotelIds.Contains(guest.HotelId)).ExecuteDeleteAsync();
        await database.RoomTypeImages.IgnoreQueryFilters()
            .Where(image => hotelIds.Contains(image.HotelId)).ExecuteDeleteAsync();
        await database.HotelImages.IgnoreQueryFilters()
            .Where(image => hotelIds.Contains(image.HotelId)).ExecuteDeleteAsync();
        await database.HotelLegalDocuments.IgnoreQueryFilters()
            .Where(document => hotelIds.Contains(document.HotelId)).ExecuteDeleteAsync();
        await database.RoomTypes.IgnoreQueryFilters()
            .Where(roomType => hotelIds.Contains(roomType.HotelId)).ExecuteDeleteAsync();
        await database.Hotels.IgnoreQueryFilters()
            .Where(hotel => hotelIds.Contains(hotel.Id)).ExecuteDeleteAsync();
        await database.HeadOffices
            .Where(headOffice => headOffice.Id == HeadOfficeId).ExecuteDeleteAsync();
    }

    private static IEnumerable<HotelLegalDocument> LegalDocuments(Guid hotelId)
    {
        string[] keys = ["terms", "privacy", "withdrawal"];

        foreach (var key in keys)
        {
            yield return new HotelLegalDocument
            {
                HotelId = hotelId,
                Key = key,
                Culture = "de",
                Version = LegalVersion,
                Title = $"IT {key}",
                BodyHtml = $"<h2>IT {key}</h2><p>Testinhalt.</p>",
                IsActive = true,
                PublishedAt = new DateTimeOffset(2026, 6, 30, 22, 0, 0, TimeSpan.Zero)
            };
        }
    }

    private static Hotel NewHotel(Guid headOfficeId, string name, string slug, bool enabled) => new()
    {
        HeadOfficeId = headOfficeId,
        Name = name,
        City = "Berlin",
        Country = Country.DE,
        Currency = "EUR",
        DefaultCulture = "de",
        AddressLine = "Chausseestrasse 5",
        PostalCode = "10115",
        Phone = "+49 30 1234567",
        Email = "info@hotelcore.test",
        VatId = "DE289176543",
        TimeZoneId = "Europe/Berlin",
        CheckInFromLocal = new TimeOnly(15, 0),
        CheckOutUntilLocal = new TimeOnly(11, 0),
        Amenities = "wifi,parking",
        PublicSlug = slug,
        TaxProfile = new TaxProfile
        {
            VatRate = 19m,
            ReducedVatRate = 7m,
            CityTaxEnabled = true,
            CityTaxPerPersonNight = CityTaxPerPersonNight,
            CityTaxExemptChildren = false,
            CityTaxChildAgeLimit = 18
        },
        PublicBookingSettings = new PublicBookingSettings
        {
            IsEnabled = enabled,
            MinNights = 1,
            MaxNights = 30,
            MaxAdvanceDays = 365,
            MinAdvanceHours = 0,
            MaxAdults = 4,
            MaxChildren = 3,
            ConfirmationMode = PublicBookingConfirmationMode.Instant
        },
        CancellationPolicy = new CancellationPolicy
        {
            Type = CancellationPolicyType.Flexible,
            FreeCancellationDaysBeforeArrival = 3,
            CutoffLocalTime = new TimeOnly(18, 0),
            LateCancellationFeePercent = 90.00m,
            NoShowFeePercent = 90.00m
        },
        LegalProfile = new HotelLegalProfile
        {
            LegalEntityName = "IT Betriebs GmbH",
            LegalForm = "GmbH",
            RepresentedBy = "Anna Becker",
            AddressLine = "Chausseestrasse 5",
            PostalCode = "10115",
            City = "Berlin",
            Country = Country.DE,
            RegisterCourt = "Amtsgericht Berlin-Charlottenburg",
            RegisterNumber = "HRB 284913 B",
            ParticipatesInDisputeResolution = false,
            OnlineDisputeResolutionUrl = "https://ec.europa.eu/consumers/odr/"
        }
    };
}
