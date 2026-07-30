using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Availability.GetAvailability;
using HotelCore.Application.Features.Reservations.Update;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Reservations;

/// <summary>
/// Musaitlik kurali: konaklama araligi <b>yari aciktir</b> <c>[checkIn, checkOut)</c>.
/// <para>
/// En kritik iki davranis burada birlikte kilitlenir:
/// <list type="bullet">
///   <item>gercek kesisim → <b>409</b>,</item>
///   <item><b>cikis gunu = giris gunu</b> (ardisik konaklama) → <b>201</b>. Bu ikinci kural
///         kolayca regresyona ugrar: kesisim kosulunda <c>&lt;</c> yerine <c>&lt;=</c> yazmak
///         oteli her gun bir gece bos birakirdi.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ReservationAvailabilityTests
{
    [Fact]
    public async Task An_overlapping_stay_in_the_same_room_is_rejected_as_a_conflict()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.CreateReservationAsync(start, start.AddDays(3));

        var act = async () => await host.CreateReservationAsync(start.AddDays(1), start.AddDays(4));

        var thrown = await act.Should().ThrowAsync<ConflictException>();

        // Mesaj metni yerellestirildi (de/en/tr) ve sozlesmenin parcasi DEGILDIR; bu yuzden
        // dile bagimli olmayan parcalar dogrulanir: hangi odanin, hangi rezervasyonla
        // cakistigini kullaniciya soylemek mesajin asil isidir.
        thrown.Which.Message.Should().Contain("RES-", "cakisan rezervasyonun numarasi mesajda olmalidir");
    }

    [Fact]
    public async Task A_stay_starting_on_the_departure_day_of_another_stay_is_accepted()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var first = await host.CreateReservationAsync(start, start.AddDays(3));

        // 13. gun ilk konaklamanin CIKIS gunudur: o gece oda bostur, satilabilir.
        var second = await host.CreateReservationAsync(start.AddDays(3), start.AddDays(5));

        second.Id.Should().NotBe(first.Id);
        second.CheckIn.Should().Be(first.CheckOut);
        second.Nights.Should().Be(2);
    }

    [Fact]
    public async Task A_stay_ending_on_the_arrival_day_of_another_stay_is_accepted()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.CreateReservationAsync(start, start.AddDays(3));

        // Ters yon: yeni konaklama mevcut olanin GIRIS gununde biter.
        var earlier = await host.CreateReservationAsync(start.AddDays(-2), start);

        earlier.CheckOut.Should().Be(start);
        earlier.Nights.Should().Be(2);
    }

    [Fact]
    public async Task A_fully_enclosed_stay_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.CreateReservationAsync(start, start.AddDays(5));

        var act = async () => await host.CreateReservationAsync(start.AddDays(1), start.AddDays(2));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.NoShow)]
    public async Task Dates_of_a_cancelled_or_no_show_reservation_can_be_sold_again(
        ReservationStatus status)
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.AddReservationAsync(host.RoomId, start, start.AddDays(3), status);

        var resold = await host.CreateReservationAsync(start, start.AddDays(3));

        resold.CheckIn.Should().Be(start);
        resold.Status.Should().Be(nameof(ReservationStatus.Option));
    }

    [Theory]
    [InlineData(ReservationStatus.Option)]
    [InlineData(ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.CheckedIn)]
    [InlineData(ReservationStatus.CheckedOut)]
    public async Task Every_other_status_still_blocks_the_room(ReservationStatus status)
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.AddReservationAsync(host.RoomId, start, start.AddDays(3), status);

        var act = async () => await host.CreateReservationAsync(start, start.AddDays(3));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task An_out_of_order_room_cannot_be_booked()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        var act = async () => await host.CreateReservationAsync(
            start,
            start.AddDays(2),
            roomId: host.OutOfOrderRoomId);

        var thrown = await act.Should().ThrowAsync<ConflictException>();

        // "out of order" teknik bir terimdir ve UC dilde de cevrilmeden birakilir.
        thrown.Which.Message.Should().Contain("out of order");
    }

    [Fact]
    public async Task An_out_of_order_room_is_absent_from_the_availability_list()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        var availability = await host.Dispatcher.Send(new GetAvailabilityRequest
        {
            From = start,
            To = start.AddDays(2)
        });

        availability.Rooms.Select(room => room.RoomId)
            .Should().BeEquivalentTo([host.RoomId, host.SecondRoomId]);
        availability.OutOfOrderRoomCount.Should().Be(1);
        availability.TotalRoomCount.Should().Be(3);
    }

    [Fact]
    public async Task Updating_a_reservation_does_not_collide_with_itself()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var reservation = await host.CreateReservationAsync(start, start.AddDays(3));

        // Ayni odada, kesisen bir araliga tasima: kendisi haric tutulmazsa 409 olurdu.
        var updated = await host.Dispatcher.Send(new UpdateReservationRequest
        {
            Id = reservation.Id,
            RoomId = host.RoomId,
            GuestId = host.GuestId,
            CheckIn = start.AddDays(1),
            CheckOut = start.AddDays(4),
            Adults = 2
        });

        updated.CheckIn.Should().Be(start.AddDays(1));
        updated.CheckOut.Should().Be(start.AddDays(4));
    }

    [Fact]
    public async Task Moving_a_reservation_onto_an_occupied_room_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var reservation = await host.CreateReservationAsync(start, start.AddDays(3));
        await host.AddReservationAsync(host.SecondRoomId, start, start.AddDays(3));

        var act = async () => await host.Dispatcher.Send(new UpdateReservationRequest
        {
            Id = reservation.Id,
            RoomId = host.SecondRoomId,
            GuestId = host.GuestId,
            CheckIn = start,
            CheckOut = start.AddDays(3),
            Adults = 2
        });

        await act.Should().ThrowAsync<ConflictException>();
    }
}
