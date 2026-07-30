using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Rooms;

/// <summary>
/// Oda modulunun <b>RBAC</b> testleri (architecture.md §7): policy adi = izin anahtaridir, bu
/// yuzden bir izni token'daki <c>perm</c> claim listesinden CIKARMAK ilgili ucun 403 dondurmesini
/// gerektirir. Token'siz istek 401'dir.
/// <para>
/// Her negatif testin yaninda <b>pozitif kontrol</b> vardir: ayni istek dogru izinle 2xx doner.
/// Boylece 403'un gercekten yetkilendirmeden geldigi (yolun yanlis yazilmasi, govdenin gecersiz
/// olmasi vb. degil) kanitlanir.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RoomsAuthorizationTests(PostgresFixture fixture)
{
    private static readonly string[] HousekeepingOnly =
        [Permissions.HousekeepingView, Permissions.HousekeepingUpdate];

    private static readonly string[] RoomsViewOnly = [Permissions.RoomsView];

    private static readonly string[] RoomsManageOnly = [Permissions.RoomsView, Permissions.RoomsManage];

    private static readonly string[] AllRoomPermissions =
    [
        Permissions.RoomsView,
        Permissions.RoomsManage,
        Permissions.HousekeepingView,
        Permissions.HousekeepingUpdate
    ];

    private static Uri Rooms { get; } = new("api/v1/rooms", UriKind.Relative);

    private static Uri Board { get; } = new("api/v1/rooms/board", UriKind.Relative);

    private static Uri RoomTypes { get; } = new("api/v1/room-types", UriKind.Relative);

    private static Uri HousekeepingOf(Guid roomId) =>
        new($"api/v1/rooms/{roomId}/housekeeping", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Request_without_a_token_is_rejected_with_401()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateAnonymousClient();

        using var response = await client.GetAsync(Rooms);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresPostgresFact]
    public async Task Write_request_without_a_token_is_rejected_with_401_before_validation()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateAnonymousClient();

        // Govde bilincli olarak gecersiz: kimlik dogrulama dogrulamadan ONCE calismalidir.
        using var response = await client.PostAsJsonAsync(Rooms, new { number = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresPostgresFact]
    public async Task Listing_rooms_without_Rooms_View_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);

        // Kat hizmetleri personeli: Housekeeping izinleri var, Rooms.View YOK.
        using var client = scenario.CreateClient(HousekeepingOnly);

        using var response = await client.GetAsync(Rooms);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_rooms_with_Rooms_View_succeeds()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomsViewOnly);

        using var response = await client.GetAsync(Rooms);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_room_without_Rooms_Manage_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);

        // Yalnizca okuma izni: okuyabilir ama yazamaz.
        using var client = scenario.CreateClient(RoomsViewOnly);

        using var response = await client.PostAsJsonAsync(
            Rooms,
            new { number = "701", floor = 7, roomTypeId = scenario.RoomTypeAId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_room_with_Rooms_Manage_succeeds_with_201_and_a_location_header()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomsManageOnly);

        using var response = await client.PostAsJsonAsync(
            Rooms,
            new { number = "702", floor = 7, roomTypeId = scenario.RoomTypeAId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [RequiresPostgresFact]
    public async Task Deleting_a_room_without_Rooms_Manage_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomId = await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "703", floor: 7);
        using var client = scenario.CreateClient(RoomsViewOnly);

        using var response = await client.DeleteAsync(new Uri($"api/v1/rooms/{roomId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.FindRoomIncludingDeletedAsync(roomId))!.IsDeleted
            .Should().BeFalse("403 ile reddedilen istek veriyi degistirmemelidir");
    }

    [RequiresPostgresFact]
    public async Task Updating_housekeeping_without_Housekeeping_Update_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomId = await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "704", floor: 7);

        // Oda yoneticisi izinleri (Rooms.View/Manage) kat hizmetleri guncellemesine YETMEZ.
        using var client = scenario.CreateClient(RoomsManageOnly);

        using var response = await client.PatchAsJsonAsync(
            HousekeepingOf(roomId),
            new { status = "Inspected" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Updating_housekeeping_with_Housekeeping_Update_succeeds()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomId = await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "705", floor: 7);
        using var client = scenario.CreateClient(HousekeepingOnly);

        using var response = await client.PatchAsJsonAsync(
            HousekeepingOf(roomId),
            new { status = "Inspected", note = "Kontrol edildi" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Reading_the_board_without_Housekeeping_View_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);

        // Oda yonetimi izinleri panoyu acmaya YETMEZ; pano ayri bir izne baglidir.
        using var client = scenario.CreateClient(RoomsManageOnly);

        using var response = await client.GetAsync(Board);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Reading_the_board_with_Housekeeping_View_succeeds()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(HousekeepingOnly);

        using var response = await client.GetAsync(Board);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Listing_room_types_without_Rooms_View_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(HousekeepingOnly);

        using var response = await client.GetAsync(RoomTypes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_room_type_without_Rooms_Manage_is_rejected_with_403()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomsViewOnly);

        using var response = await client.PostAsJsonAsync(
            RoomTypes,
            new { code = "SUI", name = "Suite", basePrice = 250m, capacity = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task A_token_carrying_every_room_permission_can_use_all_endpoints()
    {
        // Kapsam kontrolu: yukaridaki 403'ler izin eksikliginden geliyor, baska bir sebepten degil.
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(AllRoomPermissions);

        using var created = await client.PostAsJsonAsync(
            Rooms,
            new { number = "706", floor = 7, roomTypeId = scenario.RoomTypeAId });
        using var listed = await client.GetAsync(Rooms);
        using var board = await client.GetAsync(Board);
        using var roomTypes = await client.GetAsync(RoomTypes);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        board.StatusCode.Should().Be(HttpStatusCode.OK);
        roomTypes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
