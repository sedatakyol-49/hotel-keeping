using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// <b>Tenant izolasyonu:</b> bir otelin slug'iyla baska otelin verisine erisilemez.
///
/// <para>Bu, public kanalin en riskli noktasidir: uclar anonimdir ve otel yalnizca YOLDAN gelir.
/// Izolasyonun tek dayanagi <c>AppDbContext</c>'in global query filter'idir — public yolda
/// <c>IgnoreQueryFilters()</c> KULLANILMAZ, bu yuzden "baska otelin token'i" ile "olmayan token"
/// veritabani seviyesinde ayni sonucu verir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicTenantIsolationTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Room_type_of_another_hotel_is_not_reachable_through_this_hotels_slug()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        // B otelinin oda tipi kodu A'da yoktur; A'nin slug'i uzerinden istenirse 404 olmalidir.
        var response = await scenario.GetAsync($"/room-types/{PublicChannelScenario.RoomTypeCodeB}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var body = await PublicChannelScenario.ReadJsonAsync(response);
        body!.RootElement.GetProperty("code").GetString().Should().Be("ROOM_TYPE_NOT_FOUND");
    }

    [RequiresPostgresFact]
    public async Task Catalog_of_one_hotel_never_contains_another_hotels_room_types()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        var response = await scenario.GetAsync("/room-types");
        var raw = await PublicChannelScenario.ReadRawAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Should().Contain(PublicChannelScenario.RoomTypeCodeA);
        raw.Should().NotContain(
            PublicChannelScenario.RoomTypeCodeB,
            "global query filter baska otelin oda tipini fiziksel olarak gostermemelidir");
    }

    [RequiresPostgresFact]
    public async Task Hold_token_of_one_hotel_is_not_readable_through_another_hotels_slug()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        var (created, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;

        // Ayni token, B otelinin yolunda: satir tenant filtresine takilir -> 404.
        var foreignRead = await scenario.GetAsync($"/holds/{token}", scenario.SlugB);
        foreignRead.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var problem = await PublicChannelScenario.ReadJsonAsync(foreignRead);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("HOLD_NOT_FOUND");

        // Kendi otelinde ayni token calisir — testin "her sey 404" ile yesil gecmedigini kanitlar.
        var ownRead = await scenario.GetAsync($"/holds/{token}");
        ownRead.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Booking_access_token_of_one_hotel_is_not_readable_through_another_hotels_slug()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(35);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var bookingResponse = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));

        bookingResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var booking = await PublicChannelScenario.ReadJsonAsync(bookingResponse);
        var accessToken = booking!.RootElement.GetProperty("accessToken").GetString()!;

        var foreign = await scenario.GetAsync($"/bookings/{accessToken}", scenario.SlugB);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var problem = await PublicChannelScenario.ReadJsonAsync(foreign);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("BOOKING_NOT_FOUND");

        var own = await scenario.GetAsync($"/bookings/{accessToken}");
        own.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Disabled_channel_returns_404_and_does_not_reveal_the_hotel()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, channelEnabledB: false);

        var response = await scenario.GetAsync(string.Empty, scenario.SlugB);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var problem = await PublicChannelScenario.ReadJsonAsync(response);
        var code = problem!.RootElement.GetProperty("code").GetString();

        // "Slug yok", "silinmis" ve "kanal kapali" AYIRT EDILMEZ: hepsi HOTEL_NOT_FOUND.
        code.Should().Be("HOTEL_NOT_FOUND");

        var unknown = await scenario.GetAsync(string.Empty, "does-not-exist-at-all");
        using var unknownProblem = await PublicChannelScenario.ReadJsonAsync(unknown);

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownProblem!.RootElement.GetProperty("code").GetString().Should().Be(code);
    }

    [RequiresPostgresFact]
    public async Task Brand_listing_only_contains_hotels_with_an_open_channel()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, channelEnabledB: false);

        var response = await scenario.Client.GetAsync(
            new Uri($"/api/v1/public/brands/{scenario.BrandSlug}/hotels", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = await PublicChannelScenario.ReadJsonAsync(response);
        var slugs = body!.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("slug").GetString())
            .ToArray();

        slugs.Should().ContainSingle().Which.Should().Be(scenario.SlugA);
    }

    [RequiresPostgresFact]
    public async Task Brand_cover_images_are_scoped_to_their_own_hotel()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        var response = await scenario.Client.GetAsync(
            new Uri($"/api/v1/public/brands/{scenario.BrandSlug}/hotels", UriKind.Relative));

        using var body = await PublicChannelScenario.ReadJsonAsync(response);
        var items = body!.RootElement.EnumerateArray().ToArray();

        var hotelA = items.Single(item => item.GetProperty("slug").GetString() == scenario.SlugA);
        var hotelB = items.Single(item => item.GetProperty("slug").GetString() == scenario.SlugB);

        // A otelinin gorseli vardir, B'nin yoktur. Kapsam otel otel DARALTILDIGI icin A'nin
        // gorseli B'ye sizmamalidir (IgnoreQueryFilters kullanilsaydi ikisi de dolardi).
        hotelA.GetProperty("image").ValueKind.Should().NotBe(JsonValueKind.Null);
        hotelB.GetProperty("image").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
