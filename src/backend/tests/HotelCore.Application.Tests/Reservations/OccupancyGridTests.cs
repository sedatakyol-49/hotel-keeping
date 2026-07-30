using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Availability.Common;
using HotelCore.Application.Features.Availability.GetOccupancy;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Reservations;

/// <summary>
/// Doluluk grid'i (<c>oda × gece</c>). Kilitlenen davranislar: hucrelerin <b>seyrek</b> gelmesi,
/// <c>isArrival</c>/<c>isDeparture</c> bayraklarinin yari acik aralikla tutarli olmasi ve
/// aralik ust sinirinin (<see cref="AvailabilityLimits.MaxOccupancyRangeDays"/> gun) 400 uretmesi.
/// </summary>
public sealed class OccupancyGridTests
{
    [Fact]
    public async Task Only_occupied_nights_produce_cells()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.CreateReservationAsync(start.AddDays(1), start.AddDays(3));

        var grid = await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(5)
        });

        grid.Days.Should().HaveCount(5, "aralik yari aciktir: from dahil, to haric");
        grid.Days[0].Should().Be(start);
        grid.Days[^1].Should().Be(start.AddDays(4));

        var booked = grid.Rooms.Single(row => row.RoomId == host.RoomId);
        booked.Cells.Select(cell => cell.Date)
            .Should().Equal(start.AddDays(1), start.AddDays(2));

        grid.Rooms.Where(row => row.RoomId != host.RoomId)
            .Should().AllSatisfy(row => row.Cells.Should().BeEmpty("bos geceler hucre uretmez"));
    }

    [Fact]
    public async Task Arrival_and_departure_flags_sit_on_the_first_and_last_night()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.CreateReservationAsync(start, start.AddDays(3));

        var grid = await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(5)
        });

        var cells = grid.Rooms.Single(row => row.RoomId == host.RoomId).Cells;

        cells.Should().HaveCount(3);
        cells[0].IsArrival.Should().BeTrue();
        cells[0].IsDeparture.Should().BeFalse();
        cells[1].IsArrival.Should().BeFalse();
        cells[1].IsDeparture.Should().BeFalse();

        // Cikis gunu (start+3) icin hucre YOKTUR; "cikis" bayragi SON GECEYE konur.
        cells[2].Date.Should().Be(start.AddDays(2));
        cells[2].IsDeparture.Should().BeTrue();
        cells.Should().NotContain(cell => cell.Date == start.AddDays(3));
    }

    [Fact]
    public async Task Stays_are_clipped_to_the_requested_window()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.CreateReservationAsync(start.AddDays(-2), start.AddDays(4));

        var grid = await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(2)
        });

        var cells = grid.Rooms.Single(row => row.RoomId == host.RoomId).Cells;

        cells.Select(cell => cell.Date).Should().Equal(start, start.AddDays(1));
        cells.Should().AllSatisfy(cell => cell.IsArrival.Should().BeFalse());
        cells.Should().AllSatisfy(cell => cell.IsDeparture.Should().BeFalse());
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.NoShow)]
    public async Task Cancelled_and_no_show_stays_do_not_occupy_the_grid(ReservationStatus status)
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.AddReservationAsync(host.RoomId, start, start.AddDays(3), status);

        var grid = await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(3)
        });

        grid.Rooms.Should().AllSatisfy(row => row.Cells.Should().BeEmpty());
        grid.Summary.OccupiedRoomNights.Should().Be(0);
        grid.Summary.OccupancyRate.Should().Be(0m);
    }

    [Fact]
    public async Task The_summary_reports_room_nights_and_the_occupancy_rate()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.CreateReservationAsync(start, start.AddDays(2));

        var grid = await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(2)
        });

        // 3 oda (servis disi olan dahil) x 2 gece = 6 oda-gece; 2'si dolu.
        grid.Summary.RoomCount.Should().Be(3);
        grid.Summary.Days.Should().Be(2);
        grid.Summary.RoomNights.Should().Be(6);
        grid.Summary.OccupiedRoomNights.Should().Be(2);
        grid.Summary.OccupancyRate.Should().Be(33.33m);
    }

    [Fact]
    public async Task An_out_of_order_room_is_still_shown_but_flagged()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        var grid = await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(1)
        });

        grid.Rooms.Single(row => row.RoomId == host.OutOfOrderRoomId)
            .IsOutOfOrder.Should().BeTrue();
    }

    [Fact]
    public async Task A_range_at_the_limit_is_accepted_but_one_day_more_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today;

        var atLimit = await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(AvailabilityLimits.MaxOccupancyRangeDays)
        });
        atLimit.Days.Should().HaveCount(AvailabilityLimits.MaxOccupancyRangeDays);

        var act = async () => await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = start,
            To = start.AddDays(AvailabilityLimits.MaxOccupancyRangeDays + 1)
        });

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().ContainKey(nameof(GetOccupancyRequest.To));
    }

    [Fact]
    public async Task An_empty_or_inverted_range_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();

        var sameDay = async () => await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = host.Today,
            To = host.Today
        });
        var inverted = async () => await host.Dispatcher.Send(new GetOccupancyRequest
        {
            From = host.Today,
            To = host.Today.AddDays(-1)
        });

        await sameDay.Should().ThrowAsync<ValidationException>();
        await inverted.Should().ThrowAsync<ValidationException>();
    }
}
