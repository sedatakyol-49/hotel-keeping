using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.RoomTypes.Delete;
using HotelCore.Application.Features.RoomTypes.List;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Tests.RoomTypes;

/// <summary>
/// <c>DELETE /room-types/{id}</c> handler testleri.
/// <para>
/// Sozlesme: soft-delete; <b>bagli oda varsa 409</b>. "Bagli oda" yalnizca silinmemis odalari
/// kapsar — silinmis odalar global query filter sayesinde sayima girmez, aksi halde bir zamanlar
/// kullanilmis her oda tipi sonsuza kadar silinemez olurdu.
/// </para>
/// </summary>
public sealed class DeleteRoomTypeHandlerTests
{
    [Fact]
    public async Task Room_type_with_a_live_room_cannot_be_deleted()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "401");

        var act = async () => await host.Dispatcher.Send(new DeleteRoomTypeRequest(host.RoomTypeId));

        await act.Should().ThrowAsync<ConflictException>();
        (await host.FindRoomTypeIncludingDeletedAsync(host.RoomTypeId))!.IsDeleted
            .Should().BeFalse("reddedilen silme islemi satiri degistirmemelidir");
    }

    [Fact]
    public async Task Room_type_without_rooms_is_soft_deleted_and_disappears_from_the_listing()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var spare = await host.AddRoomTypeAsync(host.HotelId, "SGL", "Einzelzimmer");

        await host.Dispatcher.Send(new DeleteRoomTypeRequest(spare.Id));

        var stored = await host.FindRoomTypeIncludingDeletedAsync(spare.Id);
        stored.Should().NotBeNull("soft-delete satiri fiziksel olarak SILMEZ");
        stored!.IsDeleted.Should().BeTrue();

        var listed = await host.Dispatcher.Send(new ListRoomTypesRequest());
        listed.Should().NotContain(item => item.Id == spare.Id);
    }

    [Fact]
    public async Task Room_type_whose_only_room_was_already_deleted_can_be_deleted()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var spare = await host.AddRoomTypeAsync(host.HotelId, "SUI", "Suite");
        await host.AddRoomAsync(host.HotelId, spare.Id, "402", isDeleted: true);

        await host.Dispatcher.Send(new DeleteRoomTypeRequest(spare.Id));

        (await host.FindRoomTypeIncludingDeletedAsync(spare.Id))!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Room_of_another_hotel_does_not_block_deletion()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var spare = await host.AddRoomTypeAsync(host.HotelId, "TWN", "Zweibettzimmer");

        // Baska otelin odasi (baska oda tipine bagli) bu tipi kilitlememelidir.
        await host.AddRoomAsync(host.OtherHotelId, host.OtherHotelRoomTypeId, "403");

        await host.Dispatcher.Send(new DeleteRoomTypeRequest(spare.Id));

        (await host.FindRoomTypeIncludingDeletedAsync(spare.Id))!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Room_type_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new DeleteRoomTypeRequest(host.OtherHotelRoomTypeId));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deleting_the_room_type_keeps_its_translations_so_the_record_stays_restorable()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var spare = await host.AddRoomTypeAsync(host.HotelId, "FAM", "Familienzimmer");
        await host.AddTranslationAsync(spare.Id, "tr", "Name", "Aile Odasi");

        await host.Dispatcher.Send(new DeleteRoomTypeRequest(spare.Id));

        var translations = await host.Database.Translations
            .Where(translation => translation.EntityId == spare.Id)
            .ToListAsync();

        translations.Should().NotBeEmpty("ceviri satirlari korunur (kayit geri alinabilir olsun diye)");
    }

    [Fact]
    public async Task Room_type_with_a_room_that_is_out_of_order_still_cannot_be_deleted()
    {
        // Servis disi oda da bagli bir odadir; silinmis DEGILDIR.
        await using var host = await RoomModuleTestHost.CreateAsync();
        await host.AddRoomAsync(
            host.HotelId,
            host.RoomTypeId,
            "404",
            status: HousekeepingStatus.OutOfOrder,
            isOutOfOrder: true);

        var act = async () => await host.Dispatcher.Send(new DeleteRoomTypeRequest(host.RoomTypeId));

        await act.Should().ThrowAsync<ConflictException>();
    }
}
