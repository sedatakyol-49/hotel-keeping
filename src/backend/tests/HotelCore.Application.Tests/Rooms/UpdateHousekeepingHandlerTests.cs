using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Application.Features.Rooms.UpdateHousekeeping;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Rooms;

/// <summary>
/// <c>PATCH /rooms/{id}/housekeeping</c> handler testleri.
/// <para>
/// Sozlesmedeki degismez (api-contracts.md → "Dogrulama kurallari"):
/// <c>isOutOfOrder</c> ile <c>housekeepingStatus = OutOfOrder</c> <b>cift yonlu</b> tutarli
/// tutulur. Veritabaninda "servis disi ama durumu Clean" gibi celiskili satir olusamaz.
/// </para>
/// </summary>
public sealed class UpdateHousekeepingHandlerTests
{
    [Fact]
    public async Task Moving_a_room_to_out_of_order_also_raises_the_out_of_order_flag()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "101", status: HousekeepingStatus.Clean);

        var response = await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = room.Id,
            Status = HousekeepingStatus.OutOfOrder
        });

        response.HousekeepingStatus.Should().Be(nameof(HousekeepingStatus.OutOfOrder));
        response.IsOutOfOrder.Should().BeTrue();

        var stored = await host.FindRoomIncludingDeletedAsync(room.Id);
        stored!.IsOutOfOrder.Should().BeTrue("bayrak yaniti degil veritabani satirini de yansitmalidir");
        stored.HousekeepingStatus.Should().Be(HousekeepingStatus.OutOfOrder);
    }

    [Theory]
    [InlineData(HousekeepingStatus.Clean)]
    [InlineData(HousekeepingStatus.Dirty)]
    [InlineData(HousekeepingStatus.Inspected)]
    public async Task Leaving_out_of_order_clears_the_out_of_order_flag(HousekeepingStatus newStatus)
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(
            host.HotelId,
            host.RoomTypeId,
            "102",
            status: HousekeepingStatus.OutOfOrder,
            isOutOfOrder: true);

        var response = await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = room.Id,
            Status = newStatus
        });

        response.HousekeepingStatus.Should().Be(newStatus.ToString());
        response.IsOutOfOrder.Should().BeFalse("OutOfOrder'dan cikisin bayragi da indirmesi gerekir");

        var stored = await host.FindRoomIncludingDeletedAsync(room.Id);
        stored!.IsOutOfOrder.Should().BeFalse();
        stored.HousekeepingStatus.Should().Be(newStatus);
    }

    [Fact]
    public async Task Null_note_clears_the_existing_note()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(
            host.HotelId,
            host.RoomTypeId,
            "103",
            status: HousekeepingStatus.Dirty,
            note: "Minibar eksik");

        var response = await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = room.Id,
            Status = HousekeepingStatus.Inspected,
            Note = null
        });

        response.Note.Should().BeNull();

        var stored = await host.FindRoomIncludingDeletedAsync(room.Id);
        stored!.Note.Should().BeNull("sozlesme: note null gonderilirse mevcut not temizlenir");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_note_is_stored_as_null_instead_of_an_empty_string(string blankNote)
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "104", note: "Eski not");

        var response = await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = room.Id,
            Status = HousekeepingStatus.Clean,
            Note = blankNote
        });

        response.Note.Should().BeNull();
        (await host.FindRoomIncludingDeletedAsync(room.Id))!.Note.Should().BeNull();
    }

    [Fact]
    public async Task Note_is_trimmed_before_it_is_stored()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "105");

        var response = await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = room.Id,
            Status = HousekeepingStatus.Inspected,
            Note = "  Minibar dolduruldu  "
        });

        response.Note.Should().Be("Minibar dolduruldu");
    }

    [Fact]
    public async Task Room_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var foreignRoom = await host.AddRoomAsync(host.OtherHotelId, host.OtherHotelRoomTypeId, "901");

        // Aktif otel A; B otelinin odasi global query filter yuzunden hic gorunmez.
        var act = async () => await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = foreignRoom.Id,
            Status = HousekeepingStatus.Clean
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Undefined_status_value_is_rejected_by_validation()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "106");

        var act = async () => await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = room.Id,
            Status = (HousekeepingStatus)99
        });

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey(nameof(UpdateHousekeepingRequest.Status));
    }

    [Fact]
    public async Task Response_carries_the_enum_name_not_its_numeric_value()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "107");

        var response = await host.Dispatcher.Send(new UpdateHousekeepingRequest
        {
            Id = room.Id,
            Status = HousekeepingStatus.Dirty
        });

        // Sozlesme: "housekeepingStatus": "Dirty" — sayi DEGIL.
        response.HousekeepingStatus.Should().Be("Dirty");
        response.Should().BeOfType<RoomResponse>();
    }
}
