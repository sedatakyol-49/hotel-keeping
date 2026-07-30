using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Features.Availability.Common;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Reservations;

/// <summary>
/// Rezervasyon uclarinin uctan uca sozlesmesi: yari acik aralik, sunucu tarafli fiyat,
/// durum gecisleri ve doluluk grid'i sinirlari.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReservationsHttpContractTests(PostgresFixture fixture)
{
    private static readonly string[] FrontOfficePermissions =
    [
        Permissions.ReservationsView,
        Permissions.ReservationsCreate,
        Permissions.ReservationsCheckInOut
    ];

    private static Uri ReservationsUri { get; } = new("api/v1/reservations", UriKind.Relative);

    private static object Body(
        BookingScenario scenario,
        DateOnly checkIn,
        DateOnly checkOut,
        Guid? roomId = null,
        int adults = 2) => new
        {
            roomId = roomId ?? scenario.RoomAId,
            guestId = scenario.GuestAId,
            checkIn = checkIn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            checkOut = checkOut.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            adults,
            channel = nameof(ReservationChannel.Direct)
        };

    [RequiresPostgresFact]
    public async Task A_client_supplied_total_amount_is_ignored_and_the_price_is_computed_on_the_server()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);
        var start = scenario.Today.AddDays(10);

        using var response = await client.PostAsJsonAsync(ReservationsUri, new
        {
            roomId = scenario.RoomAId,
            guestId = scenario.GuestAId,
            checkIn = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            checkOut = start.AddDays(2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            adults = 2,
            // Sozlesmede olmayan alanlar: sessizce DUSURULMELIDIR.
            totalAmount = 1m,
            price = 1m,
            depositAmount = 0m
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ReservationResponse>();

        created!.TotalAmount.Should().Be(2 * BookingScenario.BasePrice, "tutar sunucuda hesaplanir");
        created.Nights.Should().Be(2);
        created.Currency.Should().Be("EUR");
        created.ReservationNumber.Should().StartWith("RES-");
    }

    [RequiresPostgresFact]
    public async Task Back_to_back_stays_are_allowed_but_a_real_overlap_is_a_conflict()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);
        var start = scenario.Today.AddDays(10);

        using var first = await client.PostAsJsonAsync(ReservationsUri, Body(scenario, start, start.AddDays(3)));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        // Cikis gunu = giris gunu: yari acik aralik geregi CAKISMA DEGILDIR.
        using var backToBack = await client.PostAsJsonAsync(
            ReservationsUri, Body(scenario, start.AddDays(3), start.AddDays(5)));
        backToBack.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "cikis gecesi oda bostur ve ayni gun tekrar satilabilir");

        using var overlapping = await client.PostAsJsonAsync(
            ReservationsUri, Body(scenario, start.AddDays(1), start.AddDays(2)));
        overlapping.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [RequiresPostgresFact]
    public async Task Booking_an_out_of_order_room_is_a_conflict()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);
        var start = scenario.Today.AddDays(10);

        using var response = await client.PostAsJsonAsync(
            ReservationsUri,
            Body(scenario, start, start.AddDays(2), scenario.OutOfOrderRoomAId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [RequiresPostgresFact]
    public async Task Exceeding_the_room_capacity_is_a_validation_error()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);
        var start = scenario.Today.AddDays(10);

        using var response = await client.PostAsJsonAsync(
            ReservationsUri,
            Body(scenario, start, start.AddDays(2), adults: 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Check_out_moves_the_room_to_dirty_in_the_same_transaction()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);

        // Bugun giris: erken check-in kurali devreye girmesin.
        var reservation = await scenario.CreateReservationAsync(
            scenario.Today,
            scenario.Today.AddDays(2));

        using var checkIn = await client.PostAsync(
            new Uri($"api/v1/reservations/{reservation.Id}/check-in", UriKind.Relative), content: null);
        checkIn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.FindRoomAsync(scenario.RoomAId))!.HousekeepingStatus
            .Should().Be(HousekeepingStatus.Clean, "check-in oda temizligini degistirmez");

        using var checkOut = await client.PostAsync(
            new Uri($"api/v1/reservations/{reservation.Id}/check-out", UriKind.Relative), content: null);
        checkOut.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await checkOut.Content.ReadFromJsonAsync<ReservationResponse>();
        result!.Status.Should().Be(nameof(ReservationStatus.CheckedOut));
        (await scenario.FindRoomAsync(scenario.RoomAId))!.HousekeepingStatus
            .Should().Be(HousekeepingStatus.Dirty, "cikis odayi otomatik kirli yapar");
    }

    [RequiresPostgresFact]
    public async Task Early_check_in_and_cancelling_a_checked_in_stay_are_conflicts()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);

        var future = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(3),
            scenario.Today.AddDays(5));

        using var early = await client.PostAsync(
            new Uri($"api/v1/reservations/{future.Id}/check-in", UriKind.Relative), content: null);
        early.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var today = await scenario.CreateReservationAsync(
            scenario.Today,
            scenario.Today.AddDays(2),
            roomId: scenario.SecondRoomAId);

        using var checkIn = await client.PostAsync(
            new Uri($"api/v1/reservations/{today.Id}/check-in", UriKind.Relative), content: null);
        checkIn.StatusCode.Should().Be(HttpStatusCode.OK);

        using var cancel = await client.PostAsJsonAsync(
            new Uri($"api/v1/reservations/{today.Id}/cancel", UriKind.Relative), new { reason = "Test" });
        cancel.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "otele girmis misafirin rezervasyonu iptal edilemez");
    }

    [RequiresPostgresFact]
    public async Task Check_in_requires_the_check_in_out_permission()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservation = await scenario.CreateReservationAsync(scenario.Today, scenario.Today.AddDays(2));

        using var withoutPermission = scenario.CreateClient(
            [Permissions.ReservationsView, Permissions.ReservationsCreate]);
        using var withPermission = scenario.CreateClient(FrontOfficePermissions);
        var checkInUri = new Uri($"api/v1/reservations/{reservation.Id}/check-in", UriKind.Relative);

        using var denied = await withoutPermission.PostAsync(checkInUri, content: null);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowed = await withPermission.PostAsync(checkInUri, content: null);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task The_occupancy_grid_rejects_a_range_longer_than_the_limit()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);
        var start = scenario.Today;

        var atLimit = Occupancy(start, start.AddDays(AvailabilityLimits.MaxOccupancyRangeDays));
        var overLimit = Occupancy(start, start.AddDays(AvailabilityLimits.MaxOccupancyRangeDays + 1));

        using var accepted = await client.GetAsync(atLimit);
        using var rejected = await client.GetAsync(overLimit);

        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        rejected.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            "sessizce kirpmak istemciyi yanilardi: eksik veriyi tam sanardi");
    }

    [RequiresPostgresFact]
    public async Task The_occupancy_grid_returns_sparse_cells_with_arrival_and_departure_flags()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions);
        var start = scenario.Today.AddDays(10);
        await scenario.CreateReservationAsync(start, start.AddDays(3));

        var grid = await client.GetFromJsonAsync<OccupancyResponse>(Occupancy(start, start.AddDays(5)));

        grid!.Days.Should().HaveCount(5);
        var row = grid.Rooms.Single(candidate => candidate.RoomId == scenario.RoomAId);

        row.Cells.Should().HaveCount(3, "hucreler seyrektir: yalnizca dolu geceler doner");
        row.Cells[0].IsArrival.Should().BeTrue();
        row.Cells[2].IsDeparture.Should().BeTrue();
        row.Cells.Should().NotContain(cell => cell.Date == start.AddDays(3));

        grid.Rooms.Single(candidate => candidate.RoomId == scenario.OutOfOrderRoomAId)
            .IsOutOfOrder.Should().BeTrue();
    }

    private static Uri Occupancy(DateOnly from, DateOnly to) =>
        new(
            $"api/v1/occupancy?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
            UriKind.Relative);
}
