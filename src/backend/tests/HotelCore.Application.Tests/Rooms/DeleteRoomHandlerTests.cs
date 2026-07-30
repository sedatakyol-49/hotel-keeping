using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Rooms.Delete;
using HotelCore.Application.Features.Rooms.List;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Rooms;

/// <summary>
/// <c>DELETE /rooms/{id}</c> handler testleri.
/// <para>
/// Sozlesme: silme <b>soft-delete</b>'tir ve odanin <b>gelecek tarihli</b>
/// (<c>CheckOut &gt;= bugun</c>), iptal edilmemis bir rezervasyonu varsa <b>409</b> doner.
/// Saat dondurulmustur (<see cref="TestClock"/>), boylece sinir durumlari (bugun cikis yapan
/// rezervasyon) gercek zamana bagli olmadan denetlenir.
/// </para>
/// </summary>
public sealed class DeleteRoomHandlerTests
{
    [Fact]
    public async Task Room_with_an_active_future_reservation_cannot_be_deleted()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "201");
        await host.AddReservationAsync(
            host.HotelId,
            room.Id,
            host.Clock.Today.AddDays(5),
            host.Clock.Today.AddDays(7),
            ReservationStatus.Confirmed);

        var act = async () => await host.Dispatcher.Send(new DeleteRoomRequest(room.Id));

        await act.Should().ThrowAsync<ConflictException>();
        (await host.FindRoomIncludingDeletedAsync(room.Id))!.IsDeleted
            .Should().BeFalse("reddedilen silme islemi satiri degistirmemelidir");
    }

    [Fact]
    public async Task Reservation_that_checks_out_today_still_blocks_deletion()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "202");

        // Sinir: CheckOut == bugun. Misafir henuz cikis yapmamis sayilir.
        await host.AddReservationAsync(
            host.HotelId,
            room.Id,
            host.Clock.Today.AddDays(-2),
            host.Clock.Today,
            ReservationStatus.CheckedIn);

        var act = async () => await host.Dispatcher.Send(new DeleteRoomRequest(room.Id));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Room_whose_future_reservation_was_cancelled_can_be_deleted()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "203");
        await host.AddReservationAsync(
            host.HotelId,
            room.Id,
            host.Clock.Today.AddDays(3),
            host.Clock.Today.AddDays(4),
            ReservationStatus.Cancelled);

        await host.Dispatcher.Send(new DeleteRoomRequest(room.Id));

        (await host.FindRoomIncludingDeletedAsync(room.Id))!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Room_with_only_past_and_already_invoiced_reservations_can_be_deleted()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "204");
        var reservation = await host.AddReservationAsync(
            host.HotelId,
            room.Id,
            host.Clock.Today.AddDays(-10),
            host.Clock.Today.AddDays(-1),
            ReservationStatus.CheckedOut);

        // GoBD / AO §147: faturalanmamis konaklama erisilemez hale getirilemez.
        // Faturalandiktan sonra odayi silmek kaydi erisilemez birakmaz.
        await host.AddIssuedInvoiceAsync(reservation);

        await host.Dispatcher.Send(new DeleteRoomRequest(room.Id));

        (await host.FindRoomIncludingDeletedAsync(room.Id))!.IsDeleted.Should().BeTrue();
    }

    /// <summary>
    /// Kuralin kendisini kilitler: gecmis tarihli olsa bile <b>faturalanmamis</b> rezervasyonu
    /// olan oda silinemez. Silinseydi rezervasyon zorunlu <c>Room</c> navigasyonu yuzunden
    /// listeden ve detaydan kaybolur, bir daha faturalanamaz ve tutari raporda askida kalirdi.
    /// </summary>
    [Fact]
    public async Task Room_with_a_past_but_unbilled_reservation_cannot_be_deleted()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "206");
        await host.AddReservationAsync(
            host.HotelId,
            room.Id,
            host.Clock.Today.AddDays(-10),
            host.Clock.Today.AddDays(-1),
            ReservationStatus.CheckedOut);

        var act = async () => await host.Dispatcher.Send(new DeleteRoomRequest(room.Id));

        await act.Should().ThrowAsync<ConflictException>();
        (await host.FindRoomIncludingDeletedAsync(room.Id))!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Deleted_room_is_soft_deleted_and_disappears_from_the_listing()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();
        var room = await host.AddRoomAsync(host.HotelId, host.RoomTypeId, "205");

        await host.Dispatcher.Send(new DeleteRoomRequest(room.Id));

        var stored = await host.FindRoomIncludingDeletedAsync(room.Id);
        stored.Should().NotBeNull("soft-delete satiri fiziksel olarak SILMEZ");
        stored!.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().BeCloseTo(host.Clock.UtcNow, TimeSpan.FromSeconds(1));

        var page = await host.Dispatcher.Send(new ListRoomsRequest());
        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Deleting_an_unknown_room_is_reported_as_not_found()
    {
        await using var host = await RoomModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new DeleteRoomRequest(Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
