using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// <c>EX_BookingHolds_NoOverlappingActiveHolds</c> —
/// <c>EXCLUDE USING gist ("RoomId" WITH =, daterange("CheckIn","CheckOut",'[)') WITH &amp;&amp;)
/// WHERE ("ConsumedAt" IS NULL)</c>.
///
/// <para><b>Predikatta neden zaman yok:</b> PostgreSQL kismi kisit predikatlarinda yalnizca
/// IMMUTABLE ifadelere izin verir; <c>"ExpiresAt" &gt; now()</c> yazilamaz. Bu yuzden "suresi
/// dolmus hold artik bloke etmesin" kurali kisitla degil <b>fiziksel silme</b> ile saglanir —
/// ve bu, <c>BookingHold</c>'un neden <c>ISoftDeletable</c> OLMADIGININ gerekcesidir. Asagidaki
/// testler her iki yari da dogrular.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BookingHoldConstraintTests(PostgresFixture fixture)
{
    private const string ConstraintName = "EX_BookingHolds_NoOverlappingActiveHolds";

    [RequiresPostgresFact]
    public async Task The_exclusion_constraint_exists_on_the_booking_holds_table()
    {
        await fixture.EnsureMigratedAsync();
        await using var database = fixture.CreateDbContext();

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
    public async Task Two_active_holds_for_the_same_room_and_overlapping_dates_are_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(20);

        await using var database = fixture.CreateDbContext();
        database.BookingHolds.Add(PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3)));
        await database.SaveChangesAsync();

        database.BookingHolds.Add(PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start.AddDays(2), start.AddDays(5)));
        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();

        var count = await database.BookingHolds.IgnoreQueryFilters()
            .CountAsync(hold => hold.RoomId == scenario.RoomAId);
        count.Should().Be(1);
    }

    [RequiresPostgresFact]
    public async Task Back_to_back_holds_are_allowed_because_the_range_is_half_open()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(20);

        await using var database = fixture.CreateDbContext();
        database.BookingHolds.Add(PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3)));
        database.BookingHolds.Add(PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start.AddDays(3), start.AddDays(6)));

        // Rezervasyon kisitiyla AYNI semantik: cikis gunu = giris gunu cakisma degildir.
        await database.SaveChangesAsync();

        var count = await database.BookingHolds.IgnoreQueryFilters()
            .CountAsync(hold => hold.RoomId == scenario.RoomAId);
        count.Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task A_consumed_hold_no_longer_blocks_the_room()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(20);

        await using var database = fixture.CreateDbContext();
        var reservation = PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3));
        database.Reservations.Add(reservation);

        var consumed = PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3),
            consumedAt: DateTimeOffset.UtcNow,
            consumedByReservationId: reservation.Id);
        database.BookingHolds.Add(consumed);
        await database.SaveChangesAsync();

        // Tuketilmis hold kisitin DISINDADIR: odayi artik rezervasyonun kendisi ve
        // EX_Reservations_NoOverlappingStays korur, hold'un ikinci kez bloke etmesi gereksizdir.
        database.BookingHolds.Add(PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3)));
        await database.SaveChangesAsync();

        var count = await database.BookingHolds.IgnoreQueryFilters()
            .CountAsync(hold => hold.RoomId == scenario.RoomAId);
        count.Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task A_consumed_hold_must_name_the_reservation_it_became()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(20);

        await using var database = fixture.CreateDbContext();
        var orphan = PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3),
            consumedAt: DateTimeOffset.UtcNow);

        database.BookingHolds.Add(orphan);

        // CK_BookingHolds_ConsumptionIsComplete: "tuketildi ama karsiligi yok" hali olusamaz.
        var act = async () => await database.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();

        database.ChangeTracker.Clear();
    }

    /// <summary>
    /// Suresi dolmus hold hala kisitin kapsamindadir (predikat zaman icermez) — bu yuzden
    /// yeni hold'un yazilabilmesi icin eskisinin <b>fiziksel olarak silinmesi</b> gerekir.
    /// Test hem kisitin bu davranisini hem de <c>Remove</c>'un gercekten sildigini dogrular
    /// (<c>BookingHold</c> <c>ISoftDeletable</c> DEGILDIR, aksi halde <c>ApplySoftDelete</c>
    /// silmeyi guncellemeye cevirir ve satir kisitta kalmaya devam ederdi).
    /// </summary>
    [RequiresPostgresFact]
    public async Task An_expired_hold_still_blocks_until_it_is_physically_deleted()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(20);

        await using var database = fixture.CreateDbContext();
        var expired = PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-30));
        database.BookingHolds.Add(expired);
        await database.SaveChangesAsync();

        expired.IsActiveAt(DateTimeOffset.UtcNow).Should().BeFalse("hold'un suresi dolmustur");

        database.BookingHolds.Add(PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3)));
        var blocked = async () => await database.SaveChangesAsync();
        await blocked.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();

        // Supurme adimi: hold oluşturma handler'i ayni transaction'da bunu yapar.
        var stale = await database.BookingHolds.IgnoreQueryFilters()
            .FirstAsync(hold => hold.Id == expired.Id);
        database.BookingHolds.Remove(stale);
        await database.SaveChangesAsync();

        var survived = await database.BookingHolds.IgnoreQueryFilters()
            .AnyAsync(hold => hold.Id == expired.Id);
        survived.Should().BeFalse(
            "BookingHold soft-delete EDILMEZ; aksi halde silinen satir kisitta kalir ve odayi "
            + "sonsuza dek bloke ederdi");

        database.BookingHolds.Add(PublicChannelData.Hold(
            scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(3)));
        await database.SaveChangesAsync();
    }

    /// <summary>N paralel hold istegi → tam 1 basari (misafir kanalinin asil yaris senaryosu).</summary>
    [RequiresPostgresFact]
    public async Task Concurrent_holds_for_the_last_room_produce_exactly_one_winner()
    {
        const int Attempts = 8;

        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(45);
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, Attempts).Select(async _ =>
        {
            await using var database = fixture.CreateDbContext();
            database.BookingHolds.Add(PublicChannelData.Hold(
                scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(2)));

            await barrier.Task;

            try
            {
                await database.SaveChangesAsync();

                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }).ToArray();

        barrier.SetResult();
        var results = await Task.WhenAll(attempts);

        results.Count(succeeded => succeeded).Should().Be(1);
    }
}
