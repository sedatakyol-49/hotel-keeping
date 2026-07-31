using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// Public kanalın <b>admin tarafındaki</b> eklentileri (api-contracts-public-booking.md §10).
///
/// <para><b>Yeni izin anahtarı YOKTUR:</b> rıza/hukuki anlık görüntü <c>Reservations.View</c>,
/// kanal ayarları <c>Settings.Manage</c> altındadır. Bu test o kararı da doğrular — izin
/// olmadan 403, izinle 200.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicBookingAdminViewTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Reception_sees_the_public_reference_and_the_website_channel()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var (reference, reservationId) = await CreateBookingAsync(scenario);

        using var client = scenario.CreateAdminClient(Permissions.ReservationsView);
        var response = await client.GetAsync(new Uri($"/api/v1/reservations/{reservationId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Misafir telefonda reservationNumber'i degil BU referansi soyler.
        body.RootElement.GetProperty("publicReference").GetString().Should().Be(reference);
        body.RootElement.GetProperty("channel").GetString().Should().Be("Website");

        // RES-... ic/ticari referans olarak KALIR ve admin tarafinda gorunur.
        body.RootElement.GetProperty("reservationNumber").GetString().Should().StartWith("RES-");
    }

    [RequiresPostgresFact]
    public async Task The_consent_snapshot_is_the_hotels_evidence_in_a_dispute()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var (reference, reservationId) = await CreateBookingAsync(scenario);

        using var client = scenario.CreateAdminClient(Permissions.ReservationsView);
        var response = await client.GetAsync(
            new Uri($"/api/v1/reservations/{reservationId}/public-booking", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("bookingReference").GetString().Should().Be(reference);

        // DSGVO Art. 7 Abs. 1 — hangi metnin hangi versiyonu onaylandi.
        var consents = root.GetProperty("consents");
        consents.GetProperty("termsAccepted").GetBoolean().Should().BeTrue();
        consents.GetProperty("termsVersion").GetString().Should().Be(PublicChannelScenario.LegalVersion);
        consents.GetProperty("withdrawalNoticeAcknowledged").GetBoolean().Should().BeTrue();
        consents.GetProperty("bookerIsAdult").GetBoolean().Should().BeTrue();

        // DSGVO Art. 4 Nr. 11 — pazarlama izni ON ISARETLI OLAMAZ.
        consents.GetProperty("marketingOptIn").GetBoolean().Should().BeFalse();

        // §312j Abs. 3 — dugmede gosterilen metin DOGRULANMAZ, KAYDEDILIR.
        root.GetProperty("orderButtonLabel").GetString().Should().Be("zahlungspflichtig buchen");

        // §312j Abs. 2 — dugmenin ustundeki ozetin dondurulmus kopyasi ve hash'i.
        root.GetProperty("summaryHash").GetString().Should().MatchRegex("^sha256:[0-9a-f]{64}$");
        root.GetProperty("orderSummary").GetProperty("totalPrice").GetProperty("amount")
            .GetDecimal().Should().BeGreaterThan(0m);
        root.GetProperty("price").GetProperty("totalGross").GetDecimal().Should().BeGreaterThan(0m);

        // Erisim token'i admin tarafinda da DONMEZ (yalnizca ne zaman kapandigi).
        root.TryGetProperty("accessToken", out _).Should().BeFalse();
        root.GetProperty("accessTokenExpiresAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [RequiresPostgresFact]
    public async Task A_reception_booking_has_no_public_snapshot()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        using var client = scenario.CreateAdminClient(Permissions.ReservationsView);

        // Var olmayan bir rezervasyonun public kaydi da 404'tur: "riza alinmamis" ile "riza
        // sorulmamis" ayrimi bos bir govdeye indirgenmez.
        var response = await client.GetAsync(
            new Uri($"/api/v1/reservations/{Guid.NewGuid()}/public-booking", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task The_public_snapshot_requires_the_existing_reservation_permission()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var (_, reservationId) = await CreateBookingAsync(scenario);

        using var client = scenario.CreateAdminClient(Permissions.RoomsView);
        var response = await client.GetAsync(
            new Uri($"/api/v1/reservations/{reservationId}/public-booking", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Hotel_settings_expose_the_public_channel_blocks()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        // Okuma yolu GET /hotels/{id}'dir (yazma yolu PUT /hotels/{id}/settings); gövde aynı
        // şekildedir, yani GET'ten alınan blok doğrudan PUT'a geri gönderilebilir.
        using var client = scenario.CreateAdminClient(
            canAccessAllHotels: true,
            Permissions.HotelsView,
            Permissions.SettingsManage);
        var response = await client.GetAsync(
            new Uri($"/api/v1/hotels/{scenario.HotelAId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("publicBooking").GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        root.GetProperty("publicBooking").GetProperty("slug").GetString().Should().Be(scenario.SlugA);
        root.GetProperty("cancellationPolicy").GetProperty("type").GetString().Should().Be("Flexible");
        root.GetProperty("legalProfile").GetProperty("legalEntityName").GetString()
            .Should().Be("IT Betriebs GmbH");
        root.GetProperty("timeZoneId").GetString().Should().Be("Europe/Berlin");

        // USt-IdNr. Steuernummer'dan AYRI bir alandir.
        root.GetProperty("vatId").GetString().Should().Be("DE289176543");

        // Sahne "tum kanallar" plani icerdigi icin uyari OLMAMALIDIR.
        root.GetProperty("warnings").GetArrayLength().Should().Be(0);
    }

    private static async Task<(string Reference, Guid ReservationId)> CreateBookingAsync(
        PublicChannelScenario scenario)
    {
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(180);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var response = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var booking = await PublicChannelScenario.ReadJsonAsync(response);
        var reference = booking!.RootElement.GetProperty("bookingReference").GetString()!;

        return (reference, await scenario.FindReservationIdAsync(reference));
    }
}
