using AwesomeAssertions;
using HotelCore.Application.Features.Rooms.GetBoard;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Rooms;

/// <summary>
/// <c>GET /rooms/board</c> handler testleri: kat bazli gruplama, durum sayaclari ve
/// kapsam (tenant + soft-delete).
/// </summary>
public sealed class RoomBoardHandlerTests
{
    [Fact]
    public async Task Board_groups_rooms_by_floor_in_ascending_floor_order()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "301", floor: 3);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "101", floor: 1);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "102", floor: 1);

        var board = await host.Dispatcher.Send(new GetRoomBoardRequest());

        board.Floors.Select(floor => floor.Floor).Should().Equal(1, 3);
        board.Floors[0].Rooms.Select(room => room.Number).Should().Equal("101", "102");
        board.Floors[1].Rooms.Should().ContainSingle();
    }

    [Fact]
    public async Task Board_summary_counts_every_housekeeping_state_separately()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "101", status: HousekeepingStatus.Clean);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "102", status: HousekeepingStatus.Clean);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "103", status: HousekeepingStatus.Dirty);
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "104", status: HousekeepingStatus.Inspected);
        await host.AddRoomAsync(
            host.HotelId,
            host.RoomTypeId,
            "105",
            status: HousekeepingStatus.OutOfOrder,
            isOutOfOrder: true);

        var board = await host.Dispatcher.Send(new GetRoomBoardRequest());

        board.Summary.Clean.Should().Be(2);
        board.Summary.Dirty.Should().Be(1);
        board.Summary.Inspected.Should().Be(1);
        board.Summary.OutOfOrder.Should().Be(1);
        board.Summary.Total.Should().Be(5);
    }

    [Fact]
    public async Task Board_shows_only_the_active_hotel_and_skips_deleted_rooms()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "101");
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "102", isDeleted: true);
        await host.AddRoomAsync(host.OtherHotelId, host.OtherHotelRoomTypeId, "901");

        var board = await host.Dispatcher.Send(new GetRoomBoardRequest());

        board.Summary.Total.Should().Be(1);
        board.Floors.SelectMany(floor => floor.Rooms).Select(room => room.Number).Should().Equal("101");
    }

    [Fact]
    public async Task Board_carries_the_status_as_the_enum_name()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "101", status: HousekeepingStatus.Inspected);

        var board = await host.Dispatcher.Send(new GetRoomBoardRequest());

        var card = board.Floors.Single().Rooms.Single();
        card.HousekeepingStatus.Should().Be(nameof(HousekeepingStatus.Inspected));
        card.RoomTypeCode.Should().Be("DBL");
    }

    [Fact]
    public async Task Board_of_a_hotel_without_rooms_is_empty_but_well_formed()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var board = await host.Dispatcher.Send(new GetRoomBoardRequest());

        board.Floors.Should().BeEmpty();
        board.Summary.Total.Should().Be(0);
    }
}
