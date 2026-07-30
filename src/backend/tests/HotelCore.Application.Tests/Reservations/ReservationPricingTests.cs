using AwesomeAssertions;
using HotelCore.Application.Features.Reservations.Common;
using HotelCore.Application.Features.Reservations.Create;
using HotelCore.Application.Features.Reservations.GetFolio;
using HotelCore.Application.Features.Reservations.Update;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Reservations;

/// <summary>
/// Fiyat <b>her zaman sunucuda</b> hesaplanir. Kilitlenen kurallar:
/// <list type="number">
///   <item>istemci tutar gonderemez — sozlesmede boyle bir alan <b>yoktur</b>,</item>
///   <item>tutar <b>gece gece</b> hesaplanir (sezon gecisinde dogru toplam),</item>
///   <item>oncelik: kanala ozel plan → tum kanallar plani → oda tipinin <c>BasePrice</c>'i.</item>
/// </list>
/// </summary>
public sealed class ReservationPricingTests
{
    [Fact]
    public void The_reservation_write_contract_carries_no_amount_field()
    {
        // "Istemcinin gonderdigi totalAmount yok sayilir" iddiasinin en guclu bicimi: govdede
        // boyle bir alan hic yoktur, dolayisiyla JSON'daki fazladan alan sessizce dusurulur.
        string[] createProperties =
            [.. typeof(CreateReservationRequest).GetProperties().Select(property => property.Name)];
        string[] updateProperties =
            [.. typeof(UpdateReservationRequest).GetProperties().Select(property => property.Name)];

        createProperties.Should().NotContain("TotalAmount").And.NotContain("Price");
        updateProperties.Should().NotContain("TotalAmount").And.NotContain("Price");

        typeof(IReservationWriteRequest).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("TotalAmount");
    }

    [Fact]
    public async Task Without_a_rate_plan_the_room_type_base_price_is_used_per_night()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        var reservation = await host.CreateReservationAsync(start, start.AddDays(3));

        reservation.Nights.Should().Be(3);
        reservation.TotalAmount.Should().Be(3 * BookingModuleTestHost.BasePrice);
        reservation.RatePlanId.Should().BeNull("plan yoksa BasePrice kullanilir");
    }

    [Fact]
    public async Task An_active_rate_plan_replaces_the_base_price()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var planId = await host.AddRatePlanAsync(
            host.RoomTypeId,
            "Sommer",
            price: 200m,
            validFrom: start,
            validTo: start.AddDays(30));

        var reservation = await host.CreateReservationAsync(start, start.AddDays(2));

        reservation.TotalAmount.Should().Be(400m);
        reservation.RatePlanId.Should().Be(planId);
        reservation.RatePlanName.Should().Be("Sommer");
    }

    [Fact]
    public async Task Prices_are_summed_night_by_night_across_a_season_boundary()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        // Ilk iki gece "Vorsaison" (100), ucuncu gece "Hochsaison" (300).
        await host.AddRatePlanAsync(host.RoomTypeId, "Vorsaison", 100m, start, start.AddDays(1));
        await host.AddRatePlanAsync(host.RoomTypeId, "Hochsaison", 300m, start.AddDays(2), start.AddDays(20));

        var reservation = await host.CreateReservationAsync(start, start.AddDays(3));

        // Tek plan tum konaklamaya uygulansaydi 300 ya da 900 cikardi.
        reservation.TotalAmount.Should().Be(500m);
        reservation.RatePlanName.Should().Be("Vorsaison", "raporlamada geliste gecerli plan yazilir");
    }

    [Fact]
    public async Task A_night_without_any_plan_falls_back_to_the_base_price()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        // Plan yalnizca ilk geceyi kapsiyor; ikinci gece BasePrice'a duser.
        await host.AddRatePlanAsync(host.RoomTypeId, "Nur eine Nacht", 200m, start, start);

        var reservation = await host.CreateReservationAsync(start, start.AddDays(2));

        reservation.TotalAmount.Should().Be(200m + BookingModuleTestHost.BasePrice);
    }

    [Fact]
    public async Task A_channel_specific_plan_wins_over_the_all_channels_plan()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        await host.AddRatePlanAsync(host.RoomTypeId, "BAR (alle Kanale)", 150m, start, start.AddDays(30));
        var channelPlanId = await host.AddRatePlanAsync(
            host.RoomTypeId,
            "Booking.com",
            price: 180m,
            validFrom: start,
            validTo: start.AddDays(30),
            channel: ReservationChannel.BookingCom);

        var direct = await host.CreateReservationAsync(start, start.AddDays(1));
        var ota = await host.CreateReservationAsync(
            start,
            start.AddDays(1),
            roomId: host.SecondRoomId,
            channel: ReservationChannel.BookingCom);

        direct.TotalAmount.Should().Be(150m, "Direct kanalina ozel plan yok → tum kanallar plani");
        ota.TotalAmount.Should().Be(180m);
        ota.RatePlanId.Should().Be(channelPlanId);
    }

    [Fact]
    public async Task An_inactive_plan_is_ignored_by_pricing()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.AddRatePlanAsync(
            host.RoomTypeId,
            "Pasif",
            price: 999m,
            validFrom: start,
            validTo: start.AddDays(30),
            isActive: false);

        var reservation = await host.CreateReservationAsync(start, start.AddDays(1));

        reservation.TotalAmount.Should().Be(BookingModuleTestHost.BasePrice);
    }

    [Fact]
    public async Task Changing_the_dates_recalculates_the_amount()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var reservation = await host.CreateReservationAsync(start, start.AddDays(2));
        reservation.TotalAmount.Should().Be(2 * BookingModuleTestHost.BasePrice);

        var updated = await host.Dispatcher.Send(new UpdateReservationRequest
        {
            Id = reservation.Id,
            RoomId = host.RoomId,
            GuestId = host.GuestId,
            CheckIn = start,
            CheckOut = start.AddDays(4),
            Adults = 2
        });

        updated.TotalAmount.Should().Be(4 * BookingModuleTestHost.BasePrice);
    }

    [Fact]
    public async Task The_deposit_amount_is_derived_from_the_server_side_total()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        var reservation = await host.Dispatcher.Send(new CreateReservationRequest
        {
            RoomId = host.RoomId,
            GuestId = host.GuestId,
            CheckIn = start,
            CheckOut = start.AddDays(2),
            Adults = 2,
            DepositPercent = 30m
        });

        reservation.TotalAmount.Should().Be(240m);
        reservation.DepositAmount.Should().Be(72m);
    }

    [Fact]
    public async Task The_folio_room_charge_matches_the_calculated_total()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var reservation = await host.CreateReservationAsync(start, start.AddDays(2));

        var folio = await host.Dispatcher.Send(new GetReservationFolioRequest(reservation.Id));

        folio.FolioId.Should().NotBeNull("folio konaklamanin basinda acilir");
        folio.Lines.Should().ContainSingle().Which.Type.Should().Be(nameof(InvoiceLineType.RoomCharge));
        folio.TotalGross.Should().Be(reservation.TotalAmount);

        // Brutten net/KDV ayrismasi: 240,00 / 1,07 = 224,30 (indirimli oran).
        folio.Lines[0].VatRate.Should().Be(7m);
        folio.TotalNet.Should().Be(224.30m);
        folio.TotalVat.Should().Be(15.70m);
    }
}
