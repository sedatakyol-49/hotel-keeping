using System.Globalization;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// Rezervasyon yaşam döngüsünün sözleşme davranışları: özet mutabakatı, hukuki versiyon
/// denetimi, ödeme yöntemi, iptal ve <c>lookup</c>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicBookingLifecycleTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task A_changed_summary_hash_is_rejected()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(140);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;

        var response = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, "sha256:" + new string('0', 64)));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var problem = await PublicChannelScenario.ReadJsonAsync(response);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("SUMMARY_CHANGED");
    }

    [RequiresPostgresFact]
    public async Task An_outdated_terms_version_is_rejected()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(141);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var response = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash, termsVersion: "2020-01-01"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var problem = await PublicChannelScenario.ReadJsonAsync(response);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("LEGAL_TEXT_CHANGED");
    }

    [RequiresPostgresFact]
    public async Task A_card_guarantee_request_is_answered_with_channel_not_configured()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(142);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var json = PublicChannelScenario.BookingJson(token, hash)
            .Replace("\"guarantee\":null", "\"guarantee\":\"CardGuarantee\"", StringComparison.Ordinal);

        var response = await scenario.PostRawAsync("/bookings", json);

        // Sessizce YOK SAYILMAZ: sözleşme yalan söylemez.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var problem = await PublicChannelScenario.ReadJsonAsync(response);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("CHANNEL_NOT_CONFIGURED");
    }

    [RequiresPostgresFact]
    public async Task A_missing_consent_is_a_validation_error()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(143);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var json = PublicChannelScenario.BookingJson(token, hash)
            .Replace("\"termsAccepted\":true", "\"termsAccepted\":false", StringComparison.Ordinal);

        var response = await scenario.PostRawAsync("/bookings", json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var problem = await PublicChannelScenario.ReadJsonAsync(response);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
        problem.RootElement.GetProperty("errors").EnumerateObject()
            .Select(property => property.Name)
            .Should().Contain("Consents.TermsAccepted");
    }

    [RequiresPostgresFact]
    public async Task Free_cancellation_charges_nothing_and_flips_the_status()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        // Ücretsiz iptal penceresi: girişten 3 gün öncesine kadar. 150 gün ilerisi güvenle içeride.
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(150);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var bookingResponse = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));

        using var booking = await PublicChannelScenario.ReadJsonAsync(bookingResponse);
        var accessToken = booking!.RootElement.GetProperty("accessToken").GetString()!;

        booking.RootElement.GetProperty("cancellation").GetProperty("isFreeCancellationAvailable")
            .GetBoolean().Should().BeTrue();

        var cancel = await scenario.PostRawAsync($"/bookings/{accessToken}/cancel", "{}");
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        using var cancelled = await PublicChannelScenario.ReadJsonAsync(cancel);
        cancelled!.RootElement.GetProperty("status").GetString().Should().Be("Cancelled");
        cancelled.RootElement.GetProperty("cancellation").GetProperty("chargedFeeAmount")
            .GetDecimal().Should().Be(0m);

        // İdempotent DEĞİLDİR: ikinci iptal 409 döner.
        var again = await scenario.PostRawAsync($"/bookings/{accessToken}/cancel", "{}");
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var problem = await PublicChannelScenario.ReadJsonAsync(again);
        problem!.RootElement.GetProperty("code").GetString().Should().Be("BOOKING_ALREADY_CANCELLED");
    }

    [RequiresPostgresFact]
    public async Task Acknowledging_a_fee_that_is_not_due_is_rejected()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(151);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(2));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var bookingResponse = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));

        using var booking = await PublicChannelScenario.ReadJsonAsync(bookingResponse);
        var accessToken = booking!.RootElement.GetProperty("accessToken").GetString()!;

        var cancel = await scenario.PostRawAsync(
            $"/bookings/{accessToken}/cancel",
            """{"acknowledgedFeeAmount": 99.00}""");

        // Ücretsiz iptalde tutar gönderilmesi, istemcinin yanlış ekran gösterdiğinin işaretidir.
        cancel.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Lookup_always_answers_202_without_a_body()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);

        var unknown = await scenario.PostRawAsync(
            "/bookings/lookup",
            """{"bookingReference":"K7QM-3XPD-9RTV","email":"nobody@example.com"}""");

        unknown.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await PublicChannelScenario.ReadRawAsync(unknown)).Should().BeEmpty();

        // Geçersiz biçimli referans da AYNI yanıtı alır.
        var malformed = await scenario.PostRawAsync(
            "/bookings/lookup",
            """{"bookingReference":"nope","email":"nobody@example.com"}""");

        malformed.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await PublicChannelScenario.ReadRawAsync(malformed)).Should().BeEmpty();
    }

    [RequiresPostgresFact]
    public async Task Booking_reference_is_crockford_base32_and_never_the_internal_number()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(160);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkIn.AddDays(1));
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        var response = await scenario.PostRawAsync(
            "/bookings",
            PublicChannelScenario.BookingJson(token, hash));

        using var booking = await PublicChannelScenario.ReadJsonAsync(response);
        var reference = booking!.RootElement.GetProperty("bookingReference").GetString()!;

        // 4-4-4 gruplu, Crockford alfabesi (I/L/O/U YOK).
        reference.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$");
        reference.Should().NotStartWith("RES-");

        // accessToken YALNIZCA oluşturma yanıtında; sonraki okumada null.
        var accessToken = booking.RootElement.GetProperty("accessToken").GetString()!;
        accessToken.Should().HaveLength(27);

        var read = await scenario.GetAsync($"/bookings/{accessToken}");
        using var reread = await PublicChannelScenario.ReadJsonAsync(read);
        reread!.RootElement.GetProperty("accessToken").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [RequiresPostgresFact]
    public async Task Stay_dates_come_from_the_hold_not_from_the_request()
    {
        await using var scenario = await PublicChannelScenario.StartAsync(fixture);
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(170);
        var checkOut = checkIn.AddDays(3);

        var (_, hold) = await scenario.CreateHoldAsync(checkIn, checkOut, adults: 3, children: 1);
        var token = hold!.RootElement.GetProperty("holdToken").GetString()!;
        var hash = hold.RootElement.GetProperty("orderSummary").GetProperty("hash").GetString()!;

        // Gövdeye kişi sayısı/tarih ENJEKTE ediliyor; sunucu bunları OKUMAMALIDIR.
        var json = PublicChannelScenario.BookingJson(token, hash)
            .Replace(
                "\"challengeToken\":null",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"\"challengeToken\":null,\"adults\":9,\"children\":9,\"checkIn\":\"2030-01-01\""),
                StringComparison.Ordinal);

        var response = await scenario.PostRawAsync("/bookings", json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var booking = await PublicChannelScenario.ReadJsonAsync(response);
        var stay = booking!.RootElement.GetProperty("stay");

        stay.GetProperty("adults").GetInt32().Should().Be(3);
        stay.GetProperty("children").GetInt32().Should().Be(1);
        stay.GetProperty("checkIn").GetString().Should().Be(checkIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
