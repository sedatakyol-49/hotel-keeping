using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Rooms.Create;
using HotelCore.Application.Features.Rooms.Delete;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Tests.Rooms;

/// <summary>
/// <c>POST /rooms</c> handler testleri: benzersizlik (409), oda tipi kapsami (404),
/// varsayilanlar ve <c>isOutOfOrder</c> tutarliligi.
/// </summary>
public sealed class CreateRoomHandlerTests
{
    private static CreateRoomRequest Request(Guid roomTypeId, string number, int floor = 2) => new()
    {
        Number = number,
        Floor = floor,
        RoomTypeId = roomTypeId
    };

    [Fact]
    public async Task Duplicate_room_number_in_the_same_hotel_is_rejected_as_a_conflict()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "301");

        var act = async () => await host.Dispatcher.Send(Request(host.RoomTypeId, "301"));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Room_number_uniqueness_ignores_surrounding_whitespace()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "302");

        // Numara kaydedilmeden once trim edilir; "  302  " ayni odayla cakisir.
        var act = async () => await host.Dispatcher.Send(Request(host.RoomTypeId, "  302  "));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Same_room_number_in_a_different_hotel_is_allowed()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.OtherHotelId, host.OtherHotelRoomTypeId, "303");

        var created = await host.Dispatcher.Send(Request(host.RoomTypeId, "303"));

        created.Number.Should().Be("303");
        (await host.FindRoomIncludingDeletedAsync(created.Id))!.HotelId.Should().Be(host.HotelId);
    }

    [Fact]
    public async Task Room_type_belonging_to_another_hotel_is_reported_as_not_found()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        // Aktif otel A; B otelinin oda tipiyle oda acilamaz (varligi da sizdirilmaz → 404).
        var act = async () => await host.Dispatcher.Send(Request(host.OtherHotelRoomTypeId, "304"));

        var thrown = await act.Should().ThrowAsync<NotFoundException>();
        thrown.Which.EntityName.Should().Be(nameof(HotelCore.Domain.Entities.RoomType));
    }

    [Fact]
    public async Task Unknown_room_type_is_reported_as_not_found()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(Request(Guid.NewGuid(), "305"));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task New_room_defaults_to_clean_and_not_out_of_order()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request(host.RoomTypeId, "306"));

        created.HousekeepingStatus.Should().Be(nameof(HousekeepingStatus.Clean));
        created.IsOutOfOrder.Should().BeFalse();
        created.RoomTypeCode.Should().Be("DBL");
        created.Note.Should().BeNull();
    }

    [Fact]
    public async Task Creating_a_room_with_only_the_out_of_order_flag_also_sets_the_out_of_order_status()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(new CreateRoomRequest
        {
            Number = "307",
            Floor = 3,
            RoomTypeId = host.RoomTypeId,
            IsOutOfOrder = true
        });

        // Tutarlilik degismezi diger yonde de gecerli: bayrak true ise durum OutOfOrder olur.
        created.HousekeepingStatus.Should().Be(nameof(HousekeepingStatus.OutOfOrder));
        created.IsOutOfOrder.Should().BeTrue();
    }

    [Fact]
    public async Task Creating_a_room_with_a_non_out_of_order_status_clears_a_requested_flag()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(new CreateRoomRequest
        {
            Number = "308",
            Floor = 3,
            RoomTypeId = host.RoomTypeId,
            HousekeepingStatus = HousekeepingStatus.Dirty,
            IsOutOfOrder = false
        });

        created.HousekeepingStatus.Should().Be(nameof(HousekeepingStatus.Dirty));
        created.IsOutOfOrder.Should().BeFalse();
    }

    [Fact]
    public async Task Number_of_a_soft_deleted_room_can_be_used_again()
    {
        // Regresyon: kismi unique index (WHERE NOT "IsDeleted") olmadan bu senaryo
        // benzersizlik ihlaline dusuyordu ve kapatilan bir odanin numarasi bir daha
        // kullanilamiyordu (bkz. SoftDeleteIndexExtensions).
        await using var host = await RoomModuleTestHost.CreateAsync();
        var first = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "309");
        await host.Dispatcher.Send(new DeleteRoomRequest(first.Id));

        var recreated = await host.Dispatcher.Send(Request(host.RoomTypeId, "309"));

        recreated.Id.Should().NotBe(first.Id);
        recreated.Number.Should().Be("309");
    }

    [Fact]
    public async Task Unique_room_number_index_is_really_enforced_only_among_live_rows()
    {
        // Yukaridaki "silinmis odanin numarasi tekrar kullanilabilir" testinin bos bir guvence
        // OLMADIGINI kanitlar: index gercekten var ve CANLI satirlar arasinda benzersizligi
        // zorluyor. (Ikinci canli satir handler'in on kontrolu atlanarak dogrudan eklenir.)
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "312");

        var act = async () => await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "312");

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Floor_outside_the_allowed_range_is_rejected_by_validation()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(Request(host.RoomTypeId, "310", floor: 100));

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey(nameof(CreateRoomRequest.Floor));
    }

    [Fact]
    public async Task Head_office_user_without_an_active_hotel_cannot_create_a_room()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        // Konsolide mod: HotelId null, allHotels true. Kaydin hangi otele yazilacagi belirsiz.
        host.CurrentUser.HotelId = null;
        host.CurrentUser.CanAccessAllHotels = true;

        var act = async () => await host.Dispatcher.Send(Request(host.RoomTypeId, "311"));

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey("X-Hotel-Id");
    }
}
