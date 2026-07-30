using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Rooms;

/// <summary>
/// Oda modulunun gercek PostgreSQL'e karsi dogrulanmasi gereken sozlesme davranislari:
/// dogal (numerik) siralama, soft-delete + kismi unique index regresyonu, panonun finansal
/// alan icermemesi ve konsolide modda yazma isteginin reddi.
/// <para>
/// Bu davranislarin <b>hicbiri</b> handler seviyesinde SQLite ile guvenilir sekilde
/// dogrulanamaz (siralama PostgreSQL'in <c>length()</c>/collation davranisina, 409 cevirisi
/// SQLSTATE 23505'e baglidir); bu yuzden burada yer alirlar.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RoomsContractTests(PostgresFixture fixture)
{
    private static readonly string[] RoomManagerPermissions =
    [
        Permissions.RoomsView,
        Permissions.RoomsManage,
        Permissions.HousekeepingView,
        Permissions.HousekeepingUpdate
    ];

    private static readonly string[] HousekeepingPermissions =
        [Permissions.HousekeepingView, Permissions.HousekeepingUpdate];

    private static Uri Rooms { get; } = new("api/v1/rooms", UriKind.Relative);

    private static Uri Board { get; } = new("api/v1/rooms/board", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Rooms_are_sorted_naturally_so_9_comes_before_10_and_100()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomManagerPermissions);

        // Ekleme sirasi bilincli olarak karisik: siralama SQL'den gelmelidir.
        const int floor = 4;
        string[] insertionOrder = ["100", "9", "10"];

        foreach (var number in insertionOrder)
        {
            using var created = await client.PostAsJsonAsync(
                Rooms,
                new { number, floor, roomTypeId = scenario.RoomTypeAId });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var page = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            new Uri($"api/v1/rooms?floor={floor}", UriKind.Relative));

        // Leksikografik siralama "10", "100", "9" verirdi — sozlesme dogal sira ister.
        page!.Items.Select(room => room.Number).Should().Equal("9", "10", "100");
    }

    [RequiresPostgresFact]
    public async Task Natural_sort_also_orders_by_floor_before_number()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomManagerPermissions);

        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "1001", floor: 10);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "201", floor: 2);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "2", floor: 2);

        var page = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(Rooms);

        page!.Items.Select(room => room.Number).Should().Equal("2", "201", "1001");
    }

    [RequiresPostgresFact]
    public async Task Recreating_a_room_with_the_number_of_a_soft_deleted_room_returns_201_instead_of_500()
    {
        // REGRESYON: unique index (HotelId, Number) soft-delete filtresi olmadan tanimliydi.
        // Silinen odanin numarasi tekrar kullanilmak istendiginde on kontrol (silinmis satiri
        // gormedigi icin) geciyor, INSERT 23505 ile patliyor ve kullaniciya 500 donuyordu —
        // yani kapatilan bir oda bir daha ayni numarayla acilamiyordu.
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomManagerPermissions);

        var payload = new { number = "412", floor = 4, roomTypeId = scenario.RoomTypeAId };

        using var first = await client.PostAsJsonAsync(Rooms, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstRoom = await first.Content.ReadFromJsonAsync<RoomResponse>();

        using var deleted = await client.DeleteAsync(
            new Uri($"api/v1/rooms/{firstRoom!.Id}", UriKind.Relative));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var recreated = await client.PostAsJsonAsync(Rooms, payload);

        recreated.StatusCode.Should().Be(
            HttpStatusCode.Created,
            "silinen odanin numarasi yeniden kullanilabilir olmalidir (kismi unique index)");

        var recreatedRoom = await recreated.Content.ReadFromJsonAsync<RoomResponse>();
        recreatedRoom!.Id.Should().NotBe(firstRoom.Id);
        (await scenario.FindRoomIncludingDeletedAsync(firstRoom.Id))!.IsDeleted
            .Should().BeTrue("eski satir soft-delete edilmis olarak durmalidir");
    }

    [RequiresPostgresFact]
    public async Task Duplicate_room_number_returns_409_not_500()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomManagerPermissions);
        var payload = new { number = "413", floor = 4, roomTypeId = scenario.RoomTypeAId };

        using var first = await client.PostAsJsonAsync(Rooms, payload);
        using var duplicate = await client.PostAsJsonAsync(Rooms, payload);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [RequiresPostgresFact]
    public async Task Board_payload_contains_no_price_or_currency_field()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "414", floor: 4);
        using var client = scenario.CreateClient(HousekeepingPermissions);

        using var response = await client.GetAsync(Board);
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Should().Contain("414", "pano gercekten oda dondurmelidir (bos yanit yesil sayilmaz)");

        // Ham govde uzerinde: hicbir para alani/degeri gecmez (architecture.md §7).
        payload.Should().NotContainEquivalentOf("price");
        payload.Should().NotContainEquivalentOf("currency");
        payload.Should().NotContainEquivalentOf("amount");
        payload.Should().NotContain(scenario.RoomTypeABasePrice.ToString(CultureInfo.InvariantCulture));
    }

    [RequiresPostgresFact]
    public async Task Board_groups_rooms_by_floor_and_summarises_statuses()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "415", floor: 4);
        await scenario.AddRoomAsync(
            scenario.HotelAId,
            scenario.RoomTypeAId,
            "416",
            floor: 4,
            status: HousekeepingStatus.Dirty);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "515", floor: 5);

        using var client = scenario.CreateClient(HousekeepingPermissions);

        var board = await client.GetFromJsonAsync<RoomBoardResponse>(Board);

        board!.Floors.Select(floor => floor.Floor).Should().Equal(4, 5);
        board.Summary.Total.Should().Be(3);
        board.Summary.Clean.Should().Be(2);
        board.Summary.Dirty.Should().Be(1);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_room_without_the_hotel_header_in_consolidated_mode_returns_400()
    {
        // Head Office kullanicisi X-Hotel-Id gondermezse baglam konsolidedir (HotelId = null) ve
        // kaydin hangi otele yazilacagi belirsizdir: sessizce bir otel secmek yerine 400 doner.
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(RoomManagerPermissions, [], canAccessAllHotels: true);

        using var response = await client.PostAsJsonAsync(
            Rooms,
            new { number = "417", floor = 4, roomTypeId = scenario.RoomTypeAId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("X-Hotel-Id", out _)
            .Should().BeTrue("hangi header'in eksik oldugu 'errors' sozlugunde bildirilmelidir");
    }

    [RequiresPostgresFact]
    public async Task Head_office_user_can_create_a_room_when_the_hotel_header_selects_a_hotel()
    {
        // Yukaridaki 400'un sebebinin izin/aktif otel belirsizligi oldugunu kanitlar.
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        using var client = scenario.CreateClient(
            RoomManagerPermissions,
            [],
            canAccessAllHotels: true,
            activeHotelId: scenario.HotelAId);

        using var response = await client.PostAsJsonAsync(
            Rooms,
            new { number = "418", floor = 4, roomTypeId = scenario.RoomTypeAId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [RequiresPostgresFact]
    public async Task Housekeeping_patch_keeps_the_out_of_order_flag_consistent_end_to_end()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomId = await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "419", floor: 4);
        using var client = scenario.CreateClient(HousekeepingPermissions);
        var housekeeping = new Uri($"api/v1/rooms/{roomId}/housekeeping", UriKind.Relative);

        using var toOutOfOrder = await client.PatchAsJsonAsync(
            housekeeping,
            new { status = "OutOfOrder", note = "Klima arizali" });
        var outOfOrder = await toOutOfOrder.Content.ReadFromJsonAsync<RoomResponse>();

        using var backToClean = await client.PatchAsJsonAsync(
            housekeeping,
            new { status = "Clean", note = (string?)null });
        var clean = await backToClean.Content.ReadFromJsonAsync<RoomResponse>();

        outOfOrder!.IsOutOfOrder.Should().BeTrue();
        outOfOrder.HousekeepingStatus.Should().Be(nameof(HousekeepingStatus.OutOfOrder));
        outOfOrder.Note.Should().Be("Klima arizali");

        clean!.IsOutOfOrder.Should().BeFalse();
        clean.HousekeepingStatus.Should().Be(nameof(HousekeepingStatus.Clean));
        clean.Note.Should().BeNull("note null gonderilirse mevcut not temizlenir");
    }

    [RequiresPostgresFact]
    public async Task Unknown_housekeeping_status_is_rejected_with_400()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        var roomId = await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "420", floor: 4);
        using var client = scenario.CreateClient(HousekeepingPermissions);

        using var response = await client.PatchAsJsonAsync(
            new Uri($"api/v1/rooms/{roomId}/housekeeping", UriKind.Relative),
            new { status = "Sparkling" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Search_filter_matches_the_room_number_case_insensitively()
    {
        await using var scenario = await RoomModuleScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "421a", floor: 4);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "422", floor: 4);
        using var client = scenario.CreateClient(RoomManagerPermissions);

        var page = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            new Uri("api/v1/rooms?search=421A", UriKind.Relative));

        page!.Items.Select(room => room.Number).Should().Equal("421a");
    }
}
