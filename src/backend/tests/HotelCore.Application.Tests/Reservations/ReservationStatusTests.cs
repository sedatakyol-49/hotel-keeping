using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Reservations.Cancel;
using HotelCore.Application.Features.Reservations.CheckIn;
using HotelCore.Application.Features.Reservations.CheckOut;
using HotelCore.Application.Features.Reservations.NoShow;
using HotelCore.Application.Features.Reservations.Update;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Reservations;

/// <summary>
/// Rezervasyon durum makinesi: <c>Option → Confirmed → CheckedIn → CheckedOut</c>, yan dallar
/// <c>Cancelled</c> / <c>NoShow</c>. Nihai durumlar geri cevrilemez.
/// <para>
/// Ayrica check-out'un <b>ayni transaction'da</b> odayi <c>Dirty</c>'ye tasidigi dogrulanir —
/// "cikis yapildi ama oda temiz gorunuyor" ara durumu olusamaz.
/// </para>
/// </summary>
public sealed class ReservationStatusTests
{
    [Fact]
    public async Task Check_in_moves_a_confirmed_stay_to_checked_in_and_stamps_the_time()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today,
            host.Today.AddDays(2),
            ReservationStatus.Confirmed);

        var result = await host.Dispatcher.Send(new CheckInReservationRequest(reservation));

        result.Status.Should().Be(nameof(ReservationStatus.CheckedIn));
        result.CheckedInAt.Should().Be(host.Clock.UtcNow);
    }

    [Fact]
    public async Task Check_in_before_the_arrival_date_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today.AddDays(1),
            host.Today.AddDays(3),
            ReservationStatus.Confirmed);

        var act = async () => await host.Dispatcher.Send(new CheckInReservationRequest(reservation));

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("Check-in giris tarihinden once yapilamaz");
    }

    [Fact]
    public async Task Late_check_in_is_allowed()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today.AddDays(-1),
            host.Today.AddDays(2),
            ReservationStatus.Confirmed);

        // Pozitif kontrol: kural "erken" icin gecerlidir, "gec" gelen misafir kabul edilir.
        var result = await host.Dispatcher.Send(new CheckInReservationRequest(reservation));

        result.Status.Should().Be(nameof(ReservationStatus.CheckedIn));
    }

    [Fact]
    public async Task Check_in_into_an_out_of_order_room_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.OutOfOrderRoomId,
            host.Today,
            host.Today.AddDays(2),
            ReservationStatus.Confirmed);

        var act = async () => await host.Dispatcher.Send(new CheckInReservationRequest(reservation));

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("servis disi");
    }

    [Fact]
    public async Task Check_out_marks_the_room_dirty_in_the_same_transaction()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today.AddDays(-2),
            host.Today,
            ReservationStatus.CheckedIn);

        var result = await host.Dispatcher.Send(new CheckOutReservationRequest(reservation));

        result.Status.Should().Be(nameof(ReservationStatus.CheckedOut));
        result.CheckedOutAt.Should().Be(host.Clock.UtcNow);

        var room = await host.FindRoomAsync(host.RoomId);
        room!.HousekeepingStatus.Should().Be(HousekeepingStatus.Dirty);
        room.IsOutOfOrder.Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_check_out_leaves_the_room_status_untouched()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today.AddDays(-2),
            host.Today,
            ReservationStatus.Confirmed);

        var act = async () => await host.Dispatcher.Send(new CheckOutReservationRequest(reservation));

        await act.Should().ThrowAsync<ConflictException>();

        host.Database.ChangeTracker.Clear();
        (await host.FindRoomAsync(host.RoomId))!.HousekeepingStatus
            .Should().Be(HousekeepingStatus.Clean, "durum gecisi reddedildiginde oda da degismez");
    }

    [Fact]
    public async Task Check_out_without_a_prior_check_in_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today,
            host.Today.AddDays(2),
            ReservationStatus.Option);

        var act = async () => await host.Dispatcher.Send(new CheckOutReservationRequest(reservation));

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("Gecersiz durum gecisi");
    }

    [Fact]
    public async Task A_checked_in_reservation_cannot_be_cancelled()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today,
            host.Today.AddDays(2),
            ReservationStatus.CheckedIn);

        var act = async () => await host.Dispatcher.Send(new CancelReservationRequest { Id = reservation });

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("'CheckedIn' -> 'Cancelled'");
    }

    [Fact]
    public async Task Cancelling_keeps_the_record_and_appends_the_reason_to_the_notes()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.CreateReservationAsync(
            host.Today.AddDays(5),
            host.Today.AddDays(7));

        var cancelled = await host.Dispatcher.Send(new CancelReservationRequest
        {
            Id = reservation.Id,
            Reason = "Misafir vazgecti."
        });

        cancelled.Status.Should().Be(nameof(ReservationStatus.Cancelled));
        cancelled.ReservationNumber.Should().Be(reservation.ReservationNumber, "numara korunur");
        cancelled.Notes.Should().Contain("Misafir vazgecti.");
        (await host.FindReservationAsync(reservation.Id))!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task A_checked_in_reservation_cannot_be_marked_as_no_show()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today,
            host.Today.AddDays(2),
            ReservationStatus.CheckedIn);

        var act = async () => await host.Dispatcher.Send(new MarkNoShowRequest(reservation));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData(ReservationStatus.CheckedOut)]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.NoShow)]
    public async Task A_reservation_in_a_final_status_cannot_be_edited(ReservationStatus status)
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today.AddDays(5),
            host.Today.AddDays(7),
            status);

        var act = async () => await host.Dispatcher.Send(new UpdateReservationRequest
        {
            Id = reservation,
            RoomId = host.RoomId,
            GuestId = host.GuestId,
            CheckIn = host.Today.AddDays(5),
            CheckOut = host.Today.AddDays(8),
            Adults = 2
        });

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("Degistirilebilir durumlar");
    }

    [Fact]
    public async Task A_checked_in_reservation_can_still_be_extended()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var reservation = await host.AddReservationAsync(
            host.RoomId,
            host.Today,
            host.Today.AddDays(2),
            ReservationStatus.CheckedIn);

        // Pozitif kontrol: konaklama uzatma mesrudur (nihai durum degildir).
        var updated = await host.Dispatcher.Send(new UpdateReservationRequest
        {
            Id = reservation,
            RoomId = host.RoomId,
            GuestId = host.GuestId,
            CheckIn = host.Today,
            CheckOut = host.Today.AddDays(4),
            Adults = 2
        });

        updated.CheckOut.Should().Be(host.Today.AddDays(4));
        updated.Status.Should().Be(nameof(ReservationStatus.CheckedIn));
    }

    [Fact]
    public async Task Creating_a_reservation_directly_in_a_non_initial_status_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();

        var act = async () => await host.CreateReservationAsync(
            host.Today.AddDays(5),
            host.Today.AddDays(7),
            status: ReservationStatus.CheckedIn);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
