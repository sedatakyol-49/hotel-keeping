using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.RatePlans;

/// <summary>
/// Fiyat plani cakisma kisitinin <b>veritabani</b> katmani:
/// <c>EX_RatePlans_NoOverlappingActivePlans</c>
/// (<c>EXCLUDE USING gist (RoomTypeId WITH =, COALESCE(Channel,'*') WITH =,
/// daterange(ValidFrom, ValidTo, '[]') WITH &amp;&amp;) WHERE (IsActive)</c>).
///
/// <para><b>Neden burada, handler testinde degil:</b> aralik dislama kisiti PostgreSQL'e ozgudur
/// (<c>EXCLUDE USING gist</c> + <c>daterange</c>); SQLite'ta boyle bir mekanizma <b>yoktur</b>.
/// Handler'in ON KONTROLU (<c>RatePlanReader.EnsureNoOverlapAsync</c>) SQLite uzerinde ayrica
/// test edilir; ancak eszamanli iki istek birbirinin henuz commit edilmemis satirini gormedigi
/// icin <b>tek gercek koruma</b> bu kisittir.</para>
///
/// <para><b>Yontem:</b> testler dispatcher'i degil <b>dogrudan DbContext'i</b> kullanir; boylece
/// handler'in on kontrolu atlanir ve kisitin gercekten var olup calistigi gorulur (oda modulundeki
/// "index gercekten var mi" negatif kontrolu deseni). Ihlal SQLSTATE <c>23P01</c> uretir ve
/// <c>AppDbContext</c> bunu <see cref="ConflictException"/>'a — yani 409'a — cevirir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RatePlanExclusionConstraintTests(PostgresFixture fixture)
{
    private const string ConstraintName = "EX_RatePlans_NoOverlappingActivePlans";

    private static RatePlan Plan(
        BookingScenario scenario,
        string name,
        DateOnly validFrom,
        DateOnly validTo,
        ReservationChannel? channel = null,
        bool isActive = true) => new()
        {
            HotelId = scenario.HotelAId,
            RoomTypeId = scenario.RoomTypeAId,
            Name = name,
            Price = 150m,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Channel = channel,
            IsActive = isActive
        };

    [RequiresPostgresFact]
    public async Task The_exclusion_constraint_exists_on_the_rate_plans_table()
    {
        await fixture.EnsureMigratedAsync();
        await using var database = fixture.CreateDbContext();

        // contype = 'x' → EXCLUDE kisiti. Kisit dusurulurse (ya da migration atlanirsa) asagidaki
        // davranis testleri sessizce anlamsizlasirdi; bu kontrol onu engeller.
        var exists = await database.Database
            .SqlQuery<int>($"""
                SELECT 1 AS "Value"
                FROM pg_constraint
                WHERE conname = {ConstraintName} AND contype = 'x'
                """)
            .AnyAsync();

        exists.Should().BeTrue($"'{ConstraintName}' EXCLUDE kisiti migration ile olusturulmalidir");
    }

    [RequiresPostgresFact]
    public async Task Two_overlapping_active_plans_are_rejected_by_the_database_even_without_the_precheck()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.RatePlans.Add(Plan(scenario, "Sommer", start, start.AddDays(30)));
        await database.SaveChangesAsync();

        database.RatePlans.Add(Plan(scenario, "Hochsommer", start.AddDays(10), start.AddDays(40)));
        var act = async () => await database.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.Message.Should().Contain("tarih araligiyla cakisiyor");

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task The_database_range_is_closed_so_touching_end_points_are_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.RatePlans.Add(Plan(scenario, "Vorsaison", start, start.AddDays(10)));
        await database.SaveChangesAsync();

        // daterange(..., '[]') KAPALI araliktir: uc noktada esitlik cakismadir.
        database.RatePlans.Add(Plan(scenario, "Hauptsaison", start.AddDays(10), start.AddDays(20)));
        var touching = async () => await database.SaveChangesAsync();
        await touching.Should().ThrowAsync<ConflictException>();
        database.ChangeTracker.Clear();

        // Bir gun sonrasi serbesttir (pozitif kontrol: kisit fazla genis degil).
        database.RatePlans.Add(Plan(scenario, "Hauptsaison", start.AddDays(11), start.AddDays(20)));
        await database.SaveChangesAsync();
    }

    [RequiresPostgresFact]
    public async Task Two_all_channel_plans_are_caught_although_their_channel_column_is_null()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.RatePlans.Add(Plan(scenario, "BAR 1", start, start.AddDays(30)));
        await database.SaveChangesAsync();

        // SENTINEL SART: EXCLUDE kisitinda NULL "=" ile hicbir degere - NULL'a da - esit sayilmaz.
        // Ham "Channel" kullanilsaydi iki NULL plan cakissa bile YAKALANMAZDI; COALESCE(...,'*')
        // NULL'lari birbirine esitler. Bu test tam olarak o sentinel'i korur.
        database.RatePlans.Add(Plan(scenario, "BAR 2", start.AddDays(5), start.AddDays(20)));
        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task Plans_for_different_channels_may_overlap_at_the_database_level_too()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.RatePlans.Add(Plan(scenario, "BAR", start, start.AddDays(30)));
        database.RatePlans.Add(Plan(
            scenario, "Booking.com", start, start.AddDays(30), ReservationChannel.BookingCom));

        // Kanala ozel plan ile "tum kanallar" plani cakisma SAYILMAZ (fiyat seciminde kanala ozel
        // plan once gelir). Kisit bunu engellerse urun kurali kirilirdi.
        await database.SaveChangesAsync();

        var count = await database.RatePlans.IgnoreQueryFilters()
            .CountAsync(plan => plan.RoomTypeId == scenario.RoomTypeAId);
        count.Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task An_inactive_plan_is_outside_the_partial_constraint()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.RatePlans.Add(Plan(scenario, "Aktif", start, start.AddDays(30)));
        database.RatePlans.Add(Plan(scenario, "Pasif", start, start.AddDays(30), isActive: false));

        // Kisit WHERE ("IsActive") ile kismidir: pasif planlar cakisma uretmez.
        await database.SaveChangesAsync();

        var count = await database.RatePlans.IgnoreQueryFilters()
            .CountAsync(plan => plan.RoomTypeId == scenario.RoomTypeAId);
        count.Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task Activating_a_plan_onto_an_occupied_range_is_rejected_by_the_database()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        var inactive = Plan(scenario, "Pasif", start, start.AddDays(30), isActive: false);
        database.RatePlans.Add(Plan(scenario, "Aktif", start, start.AddDays(30)));
        database.RatePlans.Add(inactive);
        await database.SaveChangesAsync();

        // Kisit UPDATE'te de calisir: pasif plani aktiflestirmek onu kisitin kapsamina sokar.
        inactive.IsActive = true;
        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();
    }
}
