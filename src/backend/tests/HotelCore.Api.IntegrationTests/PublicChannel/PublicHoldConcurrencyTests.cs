using System.Net;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// <b>Eşzamanlılık:</b> son oda için N paralel <c>POST /holds</c> → tam <b>1</b> kazanan.
///
/// <para>Kazanan sayısını belirleyen şey uygulama kodu değil <b>veritabanı kısıtıdır</b>
/// (<c>EX_BookingHolds_NoOverlappingActiveHolds</c>): ön kontrol kilit almaz, iki eşzamanlı istek
/// birbirinin henüz commit edilmemiş satırını görmez. Kısıt ihlali SQLSTATE 23P01 üretir,
/// <c>AppDbContext</c> onu <c>ConflictException</c>'a, public katman da
/// <c>409 ROOM_NO_LONGER_AVAILABLE</c>'a çevirir.</para>
///
/// <para><b>Hız sınırı neden karışmıyor:</b> her sahne kendi slug'ını üretir ve bölümleme
/// anahtarı <c>(slug, IP)</c>'dir; paralel istek sayısı <c>public.holds.create</c> eşiğinin
/// (10/dk) altında tutulur.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicHoldConcurrencyTests(PostgresFixture fixture)
{
    /// <summary>Eşiğin (10/dk) altında ama yarışı görünür kılacak kadar çok.</summary>
    private const int ParallelRequests = 8;

    [RequiresPostgresFact]
    public async Task Exactly_one_hold_wins_the_last_room()
    {
        // Tek odalı otel: "son oda" senaryosunun en saf hâli.
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, roomCountA: 1);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(120);
        var checkOut = checkIn.AddDays(2);

        var attempts = Enumerable
            .Range(0, ParallelRequests)
            .Select(_ => scenario.CreateHoldAsync(checkIn, checkOut))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        var created = results.Count(result => result.Response.StatusCode == HttpStatusCode.Created);
        var conflicts = results.Count(result => result.Response.StatusCode == HttpStatusCode.Conflict);

        created.Should().Be(1, "aynı odayı iki misafir aynı anda tutamaz");
        conflicts.Should().Be(ParallelRequests - 1);

        foreach (var result in results.Where(r => r.Response.StatusCode == HttpStatusCode.Conflict))
        {
            result.Body!.RootElement.GetProperty("code").GetString()
                .Should().Be("ROOM_NO_LONGER_AVAILABLE");
        }

        // Veritabanında da tek bir aktif hold olmalıdır.
        (await scenario.CountActiveHoldsAsync()).Should().Be(1);

        foreach (var result in results)
        {
            result.Body?.Dispose();
            result.Response.Dispose();
        }
    }

    [RequiresPostgresFact]
    public async Task Two_guests_can_hold_two_different_rooms_of_the_same_type()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, roomCountA: 2);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(121);
        var checkOut = checkIn.AddDays(2);

        var first = await scenario.CreateHoldAsync(checkIn, checkOut);
        var second = await scenario.CreateHoldAsync(checkIn, checkOut);
        var third = await scenario.CreateHoldAsync(checkIn, checkOut);

        first.Response.StatusCode.Should().Be(HttpStatusCode.Created);
        second.Response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Üçüncü misafir için oda kalmadı — kırpılmamış bir "0 oda" hatası değil, sözleşmedeki
        // tek anlamlı olay: "artık oda yok".
        third.Response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        third.Body!.RootElement.GetProperty("code").GetString().Should().Be("ROOM_NO_LONGER_AVAILABLE");
    }

    [RequiresPostgresFact]
    public async Task Releasing_a_hold_frees_the_room_immediately()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, roomCountA: 1);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(122);
        var checkOut = checkIn.AddDays(1);

        var (created, hold) = await scenario.CreateHoldAsync(checkIn, checkOut);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;

        var blocked = await scenario.CreateHoldAsync(checkIn, checkOut);
        blocked.Response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var release = await scenario.Client.DeleteAsync(
            new Uri(scenario.Path($"/holds/{token}"), UriKind.Relative));
        release.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRelease = await scenario.CreateHoldAsync(checkIn, checkOut);
        afterRelease.Response.StatusCode.Should().Be(HttpStatusCode.Created);

        // İdempotent: bilinmeyen/serbest bırakılmış token yine 204 döner (varlık sızdırılmaz).
        var again = await scenario.Client.DeleteAsync(
            new Uri(scenario.Path($"/holds/{token}"), UriKind.Relative));
        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [RequiresPostgresFact]
    public async Task A_consumed_hold_cannot_be_used_twice()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture, roomCountA: 1);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(123);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(1));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var first = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash, email: "zweite@example.de"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var problem = await PublicChannelScenario.ReadJsonAsync(second);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("HOLD_ALREADY_USED");
    }
}
