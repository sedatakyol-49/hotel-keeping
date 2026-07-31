using System.Globalization;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// Public yanıtların <b>ne taşımadığının</b> ve savunma katmanlarının testleri:
/// yasak alan sızıntısı, kart tuzak teli, hız sınırı, hukuki alanların varlığı.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicResponseSafetyTests(PostgresFixture fixture)
{
    /// <summary>
    /// architecture-public-booking.md §4.3'teki <b>yasak alan listesi</b>. Bu anahtarların hiçbiri
    /// hiçbir public yanıtta geçmemelidir.
    /// <para>
    /// <b>Neden anahtar adı taraması:</b> DTO'lar ayrı olsa bile biri yanlışlıkla
    /// <c>roomNumber</c> ekleyebilir. Test tipe değil <b>tel üzerindeki gövdeye</b> bakar —
    /// sızıntının gerçekten görüldüğü yer orasıdır.
    /// </para>
    /// </summary>
    private static readonly string[] ForbiddenKeys =
    [
        "\"roomNumber\"",
        "\"floor\"",
        "\"housekeepingStatus\"",
        "\"isOutOfOrder\"",
        "\"reservationNumber\"",
        "\"notes\"",
        "\"note\"",
        "\"roomId\"",
        "\"roomTypeId\"",
        "\"hotelId\"",
        "\"headOfficeId\"",
        "\"ratePlanId\"",
        "\"ratePlanName\"",
        "\"occupancyRate\"",
        "\"adr\"",
        "\"revPar\"",
        "\"folioId\"",
        "\"guestId\"",
        "\"accessTokenHash\"",
        "\"tokenHash\"",
        "\"clientIpHash\""
    ];

    [RequiresPostgresFact]
    public async Task No_public_response_leaks_a_forbidden_field()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(70);
        var checkOut = checkIn.AddDays(2);

        var bodies = new List<(string Label, string Json)>
        {
            ("hotel", await PublicChannelScenario.ReadRawAsync(await scenario.GetAsync(string.Empty))),
            ("legal", await PublicChannelScenario.ReadRawAsync(await scenario.GetAsync("/legal"))),
            ("catalog", await PublicChannelScenario.ReadRawAsync(await scenario.GetAsync("/room-types"))),
            ("detail", await PublicChannelScenario.ReadRawAsync(
                await scenario.GetAsync($"/room-types/{PublicChannelScenario.RoomTypeCodeA}"))),
            ("availability", await PublicChannelScenario.ReadRawAsync(await scenario.GetAsync(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&adults=2&children=0"))))
        };

        var (holdResponse, hold) = await scenario.CreateHoldAsync(checkIn, checkOut);
        bodies.Add(("hold", await PublicChannelScenario.ReadRawAsync(holdResponse)));

        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var bookingResponse = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));
        bodies.Add(("booking", await PublicChannelScenario.ReadRawAsync(bookingResponse)));

        using var booking = JsonDocument.Parse(bodies[^1].Json);
        var accessToken = booking.RootElement.GetProperty("accessToken").GetString()!;
        bodies.Add(("bookingRead", await PublicChannelScenario.ReadRawAsync(
            await scenario.GetAsync($"/bookings/{accessToken}"))));

        foreach (var (label, json) in bodies)
        {
            foreach (var forbidden in ForbiddenKeys)
            {
                json.Should().NotContain(
                    forbidden,
                    "public yanıt '{0}' yasak alan {1} taşımamalıdır",
                    label,
                    forbidden);
            }
        }
    }

    [RequiresPostgresFact]
    public async Task Available_units_are_capped_at_five()
    {
        // Altı oda: gerçek sayı 6'dır ama misafire en fazla 5 gösterilir ("5+").
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, roomCountA: 6);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(75);

        var response = await scenario.GetAsync(
            $"/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(1):yyyy-MM-dd}&adults=2");

        using var body = await PublicChannelScenario.ReadJsonAsync(response);
        var availability = body!.RootElement.GetProperty("offers").EnumerateArray()
            .First().GetProperty("availability");

        availability.GetProperty("availableUnits").GetInt32().Should().Be(5);
        availability.GetProperty("availableUnitsCapped").GetBoolean().Should().BeTrue();
    }

    [RequiresPostgresFact]
    public async Task Card_field_names_are_rejected_before_the_body_is_bound()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        // Alan İÇ İÇE bir nesnede: tarama özyinelemeli olmalıdır.
        const string json = """
                            {
                              "holdToken": "aaaaaaaaaaaaaaaaaaaaaa",
                              "guest": { "firstName": "A", "cardNumber": "4111111111111111" }
                            }
                            """;

        var response = await scenario.PostRawAsync("/bookings", json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var problem = await PublicChannelScenario.ReadJsonAsync(response);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("CARD_DATA_NOT_ACCEPTED");

        // Yanıtın kendisi de kart değerini yansıtmamalıdır.
        var raw = await PublicChannelScenario.ReadRawAsync(response);
        raw.Should().NotContain("4111111111111111");
    }

    [RequiresPostgresTheory]
    [InlineData("pan")]
    [InlineData("cvc")]
    [InlineData("cvv")]
    [InlineData("expiryMonth")]
    [InlineData("expiryYear")]
    [InlineData("cardholderName")]
    public async Task Every_card_field_name_trips_the_wire(string fieldName)
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        var json = string.Create(CultureInfo.InvariantCulture, $$"""{"{{fieldName}}":"x"}""");

        var response = await scenario.PostRawAsync("/bookings", json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var problem = await PublicChannelScenario.ReadJsonAsync(response);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("CARD_DATA_NOT_ACCEPTED");
    }

    [RequiresPostgresFact]
    public async Task Rate_limit_returns_429_with_retry_after_and_a_stable_code()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, roomCountA: 1);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(200);

        // public.holds.create eşiği 10/dk; 12 istek son ikisini kesin olarak reddettirir.
        HttpResponseMessage? limited = null;

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var (response, body) = await scenario.CreateHoldAsync(
                checkIn.AddDays(attempt),
                checkIn.AddDays(attempt + 1));

            body?.Dispose();

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
                break;
            }

            response.Dispose();
        }

        limited.Should().NotBeNull("eşiğin üstündeki istek 429 almalıdır");
        limited!.Headers.RetryAfter.Should().NotBeNull("sözleşme Retry-After'ı ZORUNLU kılar");

        using var problem = await PublicChannelScenario.ReadJsonAsync(limited);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("RATE_LIMIT_EXCEEDED");

        // detail HANGİ eşiğin aşıldığını söylememelidir (bilgi sızıntısı).
        problem.RootElement.GetProperty("detail").GetString().Should().NotContain("10");

        limited.Dispose();
    }

    [RequiresPostgresFact]
    public async Task Hold_response_always_carries_the_mandatory_legal_blocks()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(80);

        var (response, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var root = hold!.RootElement;

        // §312j Abs. 2 — düğmenin üstündeki zorunlu özet, ALAN ALAN.
        var summary = root.GetProperty("orderSummary");
        summary.GetProperty("essentialFeatures").GetProperty("roomTypeName").GetString()
            .Should().NotBeNullOrWhiteSpace();
        summary.GetProperty("duration").GetProperty("nights").GetInt32().Should().Be(2);
        summary.GetProperty("totalPrice").GetProperty("amount").GetDecimal().Should().BeGreaterThan(0m);
        summary.GetProperty("components").GetArrayLength().Should().BeGreaterThan(0);
        summary.GetProperty("hash").GetString().Should().MatchRegex("^sha256:[0-9a-f]{64}$");

        // §312g Abs. 2 Nr. 9 — cayma hakkı YOKTUR ama bildirilir.
        var withdrawal = root.GetProperty("legal").GetProperty("withdrawalRight");
        withdrawal.GetProperty("applies").GetBoolean().Should().BeFalse();
        withdrawal.GetProperty("legalBasis").GetString().Should().Contain("312g");
        withdrawal.GetProperty("noticeVersion").GetString().Should().Be(PublicChannelScenario.LegalVersion);

        // §312j Abs. 3 — düğme metni ve "birebir olmalı" bayrağı.
        var button = root.GetProperty("legal").GetProperty("orderButton");
        button.GetProperty("labelDe").GetString().Should().Be("zahlungspflichtig buchen");
        button.GetProperty("mustBeExactLabel").GetBoolean().Should().BeTrue();

        // PAngV — Kurtaxe ayrı gösterilir ama toplama dâhildir.
        var cityTax = root.GetProperty("price").GetProperty("cityTax");
        cityTax.GetProperty("includedInTotal").GetBoolean().Should().BeTrue();
        cityTax.GetProperty("chargedOnlyIfStayTakesPlace").GetBoolean().Should().BeTrue();
        cityTax.GetProperty("vatRate").GetDecimal().Should().Be(0m);

        root.GetProperty("legal").GetProperty("terms").GetProperty("version").GetString()
            .Should().Be(PublicChannelScenario.LegalVersion);
    }

    [RequiresPostgresFact]
    public async Task Legal_endpoint_serves_the_imprint_from_the_database()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        var response = await scenario.GetAsync("/legal");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = await PublicChannelScenario.ReadJsonAsync(response);
        var imprint = body!.RootElement.GetProperty("imprint");

        // §5 DDG alanları — hiçbiri koda gömülü değildir.
        imprint.GetProperty("legalEntityName").GetString().Should().Be("IT Betriebs GmbH");
        imprint.GetProperty("registerNumber").GetString().Should().Be("HRB 284913 B");

        // USt-IdNr. Hotel.VatId'den gelir, Steuernummer'dan DEĞİL.
        imprint.GetProperty("vatId").GetString().Should().Be("DE289176543");

        // §36 VSBG: katılmayan işletme de bunu BİLDİRMEK zorundadır.
        var dispute = imprint.GetProperty("disputeResolution");
        dispute.GetProperty("participatesInAdr").GetBoolean().Should().BeFalse();
        dispute.GetProperty("noticeKey").GetString().Should().Be("legal.adr.notParticipating");

        body.RootElement.GetProperty("documents").GetArrayLength().Should().Be(3);
    }

    [RequiresPostgresFact]
    public async Task Catalog_is_cacheable_but_availability_and_bookings_are_not()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(85);

        var catalog = await scenario.GetAsync("/room-types");
        catalog.Headers.CacheControl!.Public.Should().BeTrue();
        catalog.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(300));

        var availability = await scenario.GetAsync(
            $"/availability?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkIn.AddDays(1):yyyy-MM-dd}&adults=2");

        // Tarihe bağlı bir sonucun cache'lenmesi, başka bir misafire YANLIŞ fiyat göstermektir.
        availability.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [RequiresPostgresFact]
    public async Task An_admin_token_grants_nothing_extra_on_a_public_route()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        // Sahte ama biçimsel olarak Bearer bir token + başka otelin X-Hotel-Id'si:
        // ikisi de public yolda TAMAMEN yok sayılmalı, istek yine 200 dönmelidir.
        scenario.Client.DefaultRequestHeaders.Add("X-Hotel-Id", scenario.HotelBId.ToString());

        var response = await scenario.GetAsync("/room-types");
        var raw = await PublicChannelScenario.ReadRawAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "X-Hotel-Id sessizce yok sayılır (400 değil)");
        raw.Should().Contain(PublicChannelScenario.RoomTypeCodeA);
        raw.Should().NotContain(PublicChannelScenario.RoomTypeCodeB);
    }
}
