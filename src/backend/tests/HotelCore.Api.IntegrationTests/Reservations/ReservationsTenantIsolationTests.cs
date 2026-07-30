using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.RatePlans.Common;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Application.Features.Reservations.Create;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Reservations;

/// <summary>
/// Rezervasyon ve fiyat plani uclarinda multi-tenant izolasyon: baska otelin kaydi <b>404</b>,
/// <c>X-Hotel-Id</c> ile erisilemeyen otele gecis <b>403</b>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReservationsTenantIsolationTests(PostgresFixture fixture)
{
    private static readonly string[] FrontOfficePermissions =
    [
        Permissions.ReservationsView,
        Permissions.ReservationsCreate,
        Permissions.ReservationsCheckInOut,
        Permissions.RatesView,
        Permissions.RatesManage
    ];

    private static Uri ReservationsUri { get; } = new("api/v1/reservations", UriKind.Relative);

    private static async Task<Guid> CreateReservationInHotelBAsync(BookingScenario scenario)
    {
        var hotelB = scenario.CreateApplicationGraph(activeHotelId: scenario.HotelBId);

        var created = await hotelB.Dispatcher.Send(new CreateReservationRequest
        {
            RoomId = scenario.RoomBId,
            GuestId = scenario.GuestBId,
            CheckIn = scenario.Today.AddDays(10),
            CheckOut = scenario.Today.AddDays(12),
            Adults = 2
        });

        return created.Id;
    }

    [RequiresPostgresFact]
    public async Task A_reservation_of_another_hotel_is_reported_as_404_not_403()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservationInB = await CreateReservationInHotelBAsync(scenario);

        using var client = scenario.CreateClient(FrontOfficePermissions, [scenario.HotelAId]);

        using var detail = await client.GetAsync(
            new Uri($"api/v1/reservations/{reservationInB}", UriKind.Relative));
        using var checkIn = await client.PostAsync(
            new Uri($"api/v1/reservations/{reservationInB}/check-in", UriKind.Relative), content: null);
        using var folio = await client.GetAsync(
            new Uri($"api/v1/reservations/{reservationInB}/folio", UriKind.Relative));

        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        checkIn.StatusCode.Should().Be(HttpStatusCode.NotFound);
        folio.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Switching_to_a_hotel_the_user_cannot_access_is_rejected_with_403()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        await CreateReservationInHotelBAsync(scenario);

        using var client = scenario.CreateClient(
            FrontOfficePermissions,
            [scenario.HotelAId],
            activeHotelId: scenario.HotelBId);

        using var response = await client.GetAsync(ReservationsUri);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_returns_only_the_reservations_of_the_active_hotel()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var reservationInA = await scenario.CreateReservationAsync(
            scenario.Today.AddDays(10),
            scenario.Today.AddDays(12));
        await CreateReservationInHotelBAsync(scenario);

        using var client = scenario.CreateClient(FrontOfficePermissions, [scenario.HotelAId]);

        var page = await client.GetFromJsonAsync<PagedResult<ReservationResponse>>(ReservationsUri);

        page!.Items.Select(reservation => reservation.Id).Should().Equal(reservationInA.Id);
        page.TotalCount.Should().Be(1, "toplam sayac da tenant filtresine tabidir");
    }

    [RequiresPostgresFact]
    public async Task Booking_a_room_or_a_guest_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions, [scenario.HotelAId]);
        var start = scenario.Today.AddDays(10);

        using var foreignRoom = await client.PostAsJsonAsync(ReservationsUri, new
        {
            roomId = scenario.RoomBId,
            guestId = scenario.GuestAId,
            checkIn = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            checkOut = start.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            adults = 2
        });

        using var foreignGuest = await client.PostAsJsonAsync(ReservationsUri, new
        {
            roomId = scenario.RoomAId,
            guestId = scenario.GuestBId,
            checkIn = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            checkOut = start.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            adults = 2
        });

        foreignRoom.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignGuest.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task A_rate_plan_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var planInB = await scenario.AddRatePlanDirectlyAsync(
            scenario.HotelBId,
            scenario.RoomTypeBId,
            "Fremdplan",
            price: 90m,
            validFrom: scenario.Today,
            validTo: scenario.Today.AddDays(30));

        using var client = scenario.CreateClient(FrontOfficePermissions, [scenario.HotelAId]);

        using var detail = await client.GetAsync(new Uri($"api/v1/rate-plans/{planInB}", UriKind.Relative));
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await client.GetFromJsonAsync<IReadOnlyList<RatePlanResponse>>(
            new Uri("api/v1/rate-plans", UriKind.Relative));
        list!.Should().BeEmpty("baska otelin plani listelenmez");
    }

    [RequiresPostgresFact]
    public async Task A_rate_plan_cannot_be_attached_to_a_room_type_of_another_hotel()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions, [scenario.HotelAId]);

        using var response = await client.PostAsJsonAsync(new Uri("api/v1/rate-plans", UriKind.Relative), new
        {
            roomTypeId = scenario.RoomTypeBId,
            name = "Sizinti",
            price = 100m,
            validFrom = scenario.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            validTo = scenario.Today.AddDays(10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task An_overlapping_rate_plan_is_reported_as_409_with_the_conflicting_plan_name()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(FrontOfficePermissions, [scenario.HotelAId]);
        var start = scenario.Today.AddDays(10);

        using var first = await client.PostAsJsonAsync(new Uri("api/v1/rate-plans", UriKind.Relative), new
        {
            roomTypeId = scenario.RoomTypeAId,
            name = "Sommer",
            price = 150m,
            validFrom = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            validTo = start.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        using var overlapping = await client.PostAsJsonAsync(new Uri("api/v1/rate-plans", UriKind.Relative), new
        {
            roomTypeId = scenario.RoomTypeAId,
            name = "Hochsommer",
            price = 200m,
            validFrom = start.AddDays(10).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            validTo = start.AddDays(40).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });

        overlapping.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await overlapping.Content.ReadAsStringAsync()).Should().Contain("Sommer");
    }
}
