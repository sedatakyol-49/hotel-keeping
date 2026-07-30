using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Api.Services;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Rooms;

/// <summary>
/// Multi-tenant izolasyonun uctan uca testi — mimarinin en kritik guvenlik garantisi
/// (architecture.md §3).
/// <para>
/// Sahne: ayni Head Office'e bagli iki otel (A, B). Kullanicinin token'i yalnizca A otelini
/// tasir (<c>hotel</c> claim'i) ve <c>allHotels</c> false'tur. Beklenen davranis:
/// <list type="bullet">
///   <item>B otelinin odasi <c>GET /rooms/{id}</c> ile <b>404</b>'tur — 403 DEGIL: kaydin var
///         oldugu bilgisi bile sizdirilmaz,</item>
///   <item><c>X-Hotel-Id: B</c> ile kapsam degistirme girisimi <b>403</b>'tur ve endpoint hic
///         calismaz (<c>HotelContextMiddleware</c>),</item>
///   <item>liste uclari yalnizca aktif otelin satirlarini gorur.</item>
/// </list>
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RoomsTenantIsolationTests(PostgresFixture fixture)
{
    private static readonly string[] RoomPermissions =
        [Permissions.RoomsView, Permissions.RoomsManage, Permissions.HousekeepingView];

    private static Uri Rooms { get; } = new("api/v1/rooms", UriKind.Relative);

    private static Uri RoomOf(Guid roomId) => new($"api/v1/rooms/{roomId}", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Room_of_another_hotel_is_reported_as_404_not_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomInB = await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "801", floor: 8);

        // Token yalnizca A otelini tasir.
        using var client = scenario.CreateClient(RoomPermissions, [scenario.HotelAId]);

        using var response = await client.GetAsync(RoomOf(roomInB));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "baska otelin kaydi 'yok' sayilir; varligi sizdirilmaz");
    }

    [RequiresPostgresFact]
    public async Task Switching_to_a_hotel_the_user_cannot_access_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "802", floor: 8);

        // X-Hotel-Id ile B oteline gecme girisimi: erisim listesinde B yok.
        using var client = scenario.CreateClient(
            RoomPermissions,
            [scenario.HotelAId],
            activeHotelId: scenario.HotelBId);

        using var response = await client.GetAsync(Rooms);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_returns_only_the_rooms_of_the_active_hotel()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "803", floor: 8);
        await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "804", floor: 8);

        using var client = scenario.CreateClient(RoomPermissions, [scenario.HotelAId]);

        var page = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(Rooms);

        page.Should().NotBeNull();
        page!.Items.Select(room => room.Number).Should().Equal("803");
        page.TotalCount.Should().Be(1, "toplam sayac da tenant filtresine tabidir");
    }

    [RequiresPostgresFact]
    public async Task Updating_a_room_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomInB = await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "805", floor: 8);

        using var client = scenario.CreateClient(RoomPermissions, [scenario.HotelAId]);

        using var response = await client.PutAsJsonAsync(
            RoomOf(roomInB),
            new { number = "805", floor = 8, roomTypeId = scenario.RoomTypeBId, housekeepingStatus = "Clean" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Patching_housekeeping_of_a_room_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomInB = await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "806", floor: 8);

        using var client = scenario.CreateClient(
            [Permissions.HousekeepingView, Permissions.HousekeepingUpdate],
            [scenario.HotelAId]);

        using var response = await client.PatchAsJsonAsync(
            new Uri($"api/v1/rooms/{roomInB}/housekeeping", UriKind.Relative),
            new { status = "Dirty" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_room_with_a_room_type_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomPermissions, [scenario.HotelAId]);

        using var response = await client.PostAsJsonAsync(
            Rooms,
            new { number = "807", floor = 8, roomTypeId = scenario.RoomTypeBId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task User_with_access_to_both_hotels_can_switch_scope_with_the_hotel_header()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "808", floor: 8);
        await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "809", floor: 8);

        // Ayni kullanici iki otele de yetkili; header hangi otelin gorulecegini belirler.
        using var inHotelB = scenario.CreateClient(
            RoomPermissions,
            [scenario.HotelAId, scenario.HotelBId],
            activeHotelId: scenario.HotelBId);

        var page = await inHotelB.GetFromJsonAsync<PagedResult<RoomResponse>>(Rooms);

        page!.Items.Select(room => room.Number).Should().Equal("809");
    }

    [RequiresPostgresFact]
    public async Task Default_active_hotel_is_the_first_hotel_claim_when_no_header_is_sent()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "810", floor: 8);
        await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "811", floor: 8);

        // Sira anlamlidir: ilk "hotel" claim'i varsayilan oteldir (burada B).
        using var client = scenario.CreateClient(RoomPermissions, [scenario.HotelBId, scenario.HotelAId]);

        var page = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(Rooms);

        page!.Items.Select(room => room.Number).Should().Equal("811");
    }

    [RequiresPostgresFact]
    public async Task Head_office_user_sees_both_hotels_in_consolidated_mode()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "812", floor: 8);
        await scenario.AddRoomAsync(scenario.HotelBId, scenario.RoomTypeBId, "813", floor: 8);

        // allHotels = true ve X-Hotel-Id yok → konsolide okuma (tenant filtresi bypass).
        using var client = scenario.CreateClient(RoomPermissions, [], canAccessAllHotels: true);

        // Konsolide modda TUM otellerin odalari doner; bu yuzden sayfa buyuk tutulup
        // yalnizca iki otelin de gorundugu dogrulanir (esitlik iddiasi kirilgan olurdu).
        var page = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            new Uri("api/v1/rooms?pageSize=200", UriKind.Relative));

        page!.Items.Select(room => room.Number).Should().Contain(["812", "813"]);
    }

    [RequiresPostgresFact]
    public async Task Malformed_hotel_header_is_rejected_with_400()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomPermissions, [scenario.HotelAId]);
        client.DefaultRequestHeaders.Add(CurrentUser.HotelHeaderName, "not-a-guid");

        using var response = await client.GetAsync(Rooms);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
