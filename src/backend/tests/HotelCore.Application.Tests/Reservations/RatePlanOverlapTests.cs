using System.Globalization;
using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.RatePlans.Create;
using HotelCore.Application.Features.RatePlans.Update;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Reservations;

/// <summary>
/// Fiyat plani <b>tarih araligi cakismasi</b> — handler'in ON KONTROLU (409, mesajda cakisan
/// planin adi ve araligi).
/// <para>
/// <b>Kapsam notu:</b> ayni kural veritabaninda <c>EXCLUDE USING gist</c> kisitiyla da zorlanir.
/// O katman <b>SQLite'ta yoktur</b> (aralik dislama kisiti PostgreSQL'e ozgudur), bu yuzden burada
/// yesil gosterilmez; integration testinde gercek PostgreSQL'e karsi dogrulanir
/// (<c>RatePlanExclusionConstraintTests</c>).
/// </para>
/// </summary>
public sealed class RatePlanOverlapTests
{
    private static CreateRatePlanRequest Plan(
        Guid roomTypeId,
        string name,
        DateOnly from,
        DateOnly to,
        ReservationChannel? channel = null,
        bool? isActive = null) => new()
        {
            RoomTypeId = roomTypeId,
            Name = name,
            Price = 150m,
            ValidFrom = from,
            ValidTo = to,
            Channel = channel,
            IsActive = isActive
        };

    [Fact]
    public async Task An_overlapping_active_plan_is_rejected_and_the_message_names_the_conflict()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.Dispatcher.Send(Plan(host.RoomTypeId, "Sommer", start, start.AddDays(30)));

        var act = async () => await host.Dispatcher.Send(
            Plan(host.RoomTypeId, "Hochsommer", start.AddDays(10), start.AddDays(40)));

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("'Sommer'");
        thrown.Which.Message.Should().Contain(start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task The_validity_range_is_closed_so_touching_end_points_conflict()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.Dispatcher.Send(Plan(host.RoomTypeId, "Vorsaison", start, start.AddDays(10)));

        // Fiyat plani bir GUN kumesidir (rezervasyonun GECE kumesinden farkli): uc noktada
        // esitlik CAKISMADIR — o gun icin iki fiyat gecerli olurdu.
        var act = async () => await host.Dispatcher.Send(
            Plan(host.RoomTypeId, "Hauptsaison", start.AddDays(10), start.AddDays(20)));

        await act.Should().ThrowAsync<ConflictException>();

        // Bir gun sonrasi serbesttir.
        var adjacent = await host.Dispatcher.Send(
            Plan(host.RoomTypeId, "Hauptsaison", start.AddDays(11), start.AddDays(20)));
        adjacent.ValidFrom.Should().Be(start.AddDays(11));
    }

    [Fact]
    public async Task Plans_for_different_channels_may_overlap()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.Dispatcher.Send(Plan(host.RoomTypeId, "BAR", start, start.AddDays(30)));

        // Kanala ozel plan ile "tum kanallar" plani cakisma SAYILMAZ: fiyat seciminde kanala
        // ozel plan her zaman once gelir, dolayisiyla belirsizlik yoktur.
        var otaPlan = await host.Dispatcher.Send(Plan(
            host.RoomTypeId,
            "Booking.com",
            start,
            start.AddDays(30),
            ReservationChannel.BookingCom));

        otaPlan.Channel.Should().Be(nameof(ReservationChannel.BookingCom));
    }

    [Fact]
    public async Task Two_plans_for_the_same_channel_still_conflict()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.Dispatcher.Send(Plan(
            host.RoomTypeId, "Booking A", start, start.AddDays(30), ReservationChannel.BookingCom));

        var act = async () => await host.Dispatcher.Send(Plan(
            host.RoomTypeId, "Booking B", start.AddDays(5), start.AddDays(35), ReservationChannel.BookingCom));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task An_inactive_plan_neither_conflicts_nor_is_blocked()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        await host.Dispatcher.Send(Plan(host.RoomTypeId, "Aktif", start, start.AddDays(30)));

        var inactive = await host.Dispatcher.Send(
            Plan(host.RoomTypeId, "Pasif", start, start.AddDays(30), isActive: false));

        inactive.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Reactivating_a_plan_onto_an_occupied_range_is_rejected()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var inactive = await host.Dispatcher.Send(
            Plan(host.RoomTypeId, "Pasif", start, start.AddDays(30), isActive: false));
        await host.Dispatcher.Send(Plan(host.RoomTypeId, "Aktif", start, start.AddDays(30)));

        var act = async () => await host.Dispatcher.Send(new UpdateRatePlanRequest
        {
            Id = inactive.Id,
            RoomTypeId = host.RoomTypeId,
            Name = "Pasif",
            Price = 150m,
            ValidFrom = start,
            ValidTo = start.AddDays(30),
            IsActive = true
        });

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("'Aktif'");
    }

    [Fact]
    public async Task Updating_a_plan_without_moving_it_does_not_conflict_with_itself()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);
        var plan = await host.Dispatcher.Send(Plan(host.RoomTypeId, "Sommer", start, start.AddDays(30)));

        var updated = await host.Dispatcher.Send(new UpdateRatePlanRequest
        {
            Id = plan.Id,
            RoomTypeId = host.RoomTypeId,
            Name = "Sommer 2026",
            Price = 175m,
            ValidFrom = start,
            ValidTo = start.AddDays(30)
        });

        updated.Name.Should().Be("Sommer 2026");
        updated.Price.Should().Be(175m);
    }

    [Fact]
    public async Task An_inverted_validity_range_is_rejected_by_validation()
    {
        await using var host = await BookingModuleTestHost.CreateAsync();
        var start = host.Today.AddDays(10);

        var act = async () => await host.Dispatcher.Send(
            Plan(host.RoomTypeId, "Ters", start.AddDays(5), start));

        await act.Should().ThrowAsync<ValidationException>();
    }
}
