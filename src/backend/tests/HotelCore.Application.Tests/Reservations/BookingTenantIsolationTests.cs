using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.RatePlans.Create;
using HotelCore.Application.Features.RatePlans.GetById;
using HotelCore.Application.Features.Reservations.Create;
using HotelCore.Application.Features.Reservations.GetById;
using HotelCore.Application.Features.Reservations.List;
using HotelCore.Application.Tests.Support;

namespace HotelCore.Application.Tests.Reservations;

/// <summary>
/// Multi-tenant izolasyon (architecture.md §3): baska otelin rezervasyonu, misafiri ve fiyat
/// plani <b>404</b>'tur — 403 degil, cunku kaydin var oldugu bilgisi bile sizdirilmaz.
/// <para>
/// Kapsam disi: <c>X-Hotel-Id</c> ile kapsam degistirme girisimi bir <b>HTTP/middleware</b>
/// davranisidir (403) ve handler seviyesinde gozlenemez; integration testinde dogrulanir.
/// Faturalarin tenant izolasyonu da integration tarafindadir (bkz. <c>InvoicesTenantIsolationTests</c>).
/// </para>
/// </summary>
public sealed class BookingTenantIsolationTests
{
    [Fact]
    public async Task A_reservation_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var foreign = await host.AddReservationAsync(
            host.OtherHotelRoomId,
            host.Today.AddDays(5),
            host.Today.AddDays(7),
            hotelId: host.OtherHotelId,
            guestId: host.OtherHotelGuestId);

        var act = async () => await host.Dispatcher.Send(new GetReservationByIdRequest(foreign));

        var thrown = await act.Should().ThrowAsync<NotFoundException>();
        thrown.Which.EntityName.Should().Be("Reservation");
    }

    [Fact]
    public async Task Listing_reservations_only_returns_the_active_hotel()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        await host.CreateReservationAsync(host.Today.AddDays(5), host.Today.AddDays(7));
        await host.AddReservationAsync(
            host.OtherHotelRoomId,
            host.Today.AddDays(5),
            host.Today.AddDays(7),
            hotelId: host.OtherHotelId,
            guestId: host.OtherHotelGuestId);

        var page = await host.Dispatcher.Send(new ListReservationsRequest());

        page.Items.Should().ContainSingle();
        page.TotalCount.Should().Be(1, "toplam sayac da tenant filtresine tabidir");
    }

    [Fact]
    public async Task Booking_a_room_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new CreateReservationRequest
        {
            RoomId = host.OtherHotelRoomId,
            GuestId = host.GuestId,
            CheckIn = host.Today.AddDays(5),
            CheckOut = host.Today.AddDays(7)
        });

        var thrown = await act.Should().ThrowAsync<NotFoundException>();
        thrown.Which.EntityName.Should().Be("Room");
    }

    [Fact]
    public async Task Booking_for_a_guest_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new CreateReservationRequest
        {
            RoomId = host.RoomId,
            GuestId = host.OtherHotelGuestId,
            CheckIn = host.Today.AddDays(5),
            CheckOut = host.Today.AddDays(7)
        });

        var thrown = await act.Should().ThrowAsync<NotFoundException>();
        thrown.Which.EntityName.Should().Be("Guest");
    }

    [Fact]
    public async Task A_rate_plan_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var foreignPlan = await host.AddRatePlanAsync(
            host.OtherHotelRoomTypeId,
            "Fremdplan",
            price: 90m,
            validFrom: host.Today,
            validTo: host.Today.AddDays(30),
            hotelId: host.OtherHotelId);

        var act = async () => await host.Dispatcher.Send(new GetRatePlanByIdRequest(foreignPlan));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task A_rate_plan_cannot_be_attached_to_a_room_type_of_another_hotel()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new CreateRatePlanRequest
        {
            RoomTypeId = host.OtherHotelRoomTypeId,
            Name = "Sizinti",
            Price = 100m,
            ValidFrom = host.Today,
            ValidTo = host.Today.AddDays(10)
        });

        var thrown = await act.Should().ThrowAsync<NotFoundException>();
        thrown.Which.EntityName.Should().Be("RoomType");
    }

    [Fact]
    public async Task Writing_without_an_active_hotel_is_rejected_as_a_validation_error()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();

        // Head Office konsolide modu: hangi otele yazilacagi belirsiz → 400.
        host.CurrentUser.HotelId = null;
        host.CurrentUser.CanAccessAllHotels = true;

        var act = async () => await host.Dispatcher.Send(new CreateReservationRequest
        {
            RoomId = host.RoomId,
            GuestId = host.GuestId,
            CheckIn = host.Today.AddDays(5),
            CheckOut = host.Today.AddDays(7)
        });

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey("X-Hotel-Id");
    }
}
