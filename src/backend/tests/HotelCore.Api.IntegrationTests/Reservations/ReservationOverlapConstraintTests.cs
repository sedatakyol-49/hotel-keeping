using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.Reservations;

/// <summary>
/// <b>Cift rezervasyon (double booking) kisiti:</b>
/// <c>EX_Reservations_NoOverlappingStays</c> —
/// <c>EXCLUDE USING gist ("RoomId" WITH =, daterange("CheckIn","CheckOut",'[)') WITH &amp;&amp;)
/// WHERE ("Status" NOT IN ('Cancelled','NoShow') AND NOT "IsDeleted")</c>.
///
/// <para><b>Neden bu kisit var:</b> bu migration'dan once ayni odayi ayni tarihe iki kez satmayi
/// engelleyen TEK sey <c>AvailabilityService</c>'in <b>kilitsiz</b> on kontroluydu. Iki eszamanli
/// istek birbirinin henuz commit edilmemis satirini gormedigi icin ikisi de on kontrolu geciyor ve
/// ikisi de yaziliyordu — klasik check-then-act yarisi. Bu, misafir kanali hic olmasa da bir
/// hatadir: iki resepsiyonist ayni anda kaydettiginde olusur.</para>
///
/// <para><b>Yontem:</b> testler dispatcher'i degil <b>dogrudan DbContext'i</b> kullanir; boylece
/// handler'in on kontrolu atlanir ve kisitin gercekten var olup calistigi gorulur
/// (<c>RatePlanExclusionConstraintTests</c> ile ayni desen). Ihlal SQLSTATE <c>23P01</c> uretir ve
/// <c>AppDbContext</c> bunu <see cref="ConflictException"/>'a — yani 409'a — cevirir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReservationOverlapConstraintTests(PostgresFixture fixture)
{
    private const string ConstraintName = "EX_Reservations_NoOverlappingStays";

    [RequiresPostgresFact]
    public async Task The_exclusion_constraint_exists_on_the_reservations_table()
    {
        await fixture.EnsureMigratedAsync();
        await using var database = fixture.CreateDbContext();

        // contype = 'x' -> EXCLUDE kisiti. Kisit dusurulurse asagidaki davranis testleri sessizce
        // anlamsizlasirdi; bu kontrol onu engeller.
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
    public async Task Two_overlapping_stays_in_the_same_room_are_rejected_by_the_database()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3)));
        await database.SaveChangesAsync();

        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start.AddDays(1), start.AddDays(4)));
        var act = async () => await database.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<ConflictException>();

        // Sema detayi (kisit adi) kullaniciya sizmaz — mesaj yerellestirilmis kaynaktan gelir.
        thrown.Which.Message.Should().NotContain(ConstraintName);

        database.ChangeTracker.Clear();

        var count = await database.Reservations.IgnoreQueryFilters()
            .CountAsync(reservation => reservation.RoomId == scenario.RoomAId);
        count.Should().Be(1, "cakisan ikinci rezervasyon veritabanina yazilmamalidir");
    }

    [RequiresPostgresFact]
    public async Task Back_to_back_stays_are_allowed_because_the_range_is_half_open()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3)));
        await database.SaveChangesAsync();

        // daterange(..., '[)') YARI ACIK araliktir: bir konaklamanin cikis gunu, ayni odada baska
        // bir konaklamanin giris gunu olabilir. RatePlans'teki '[]' KAPALI araliktan farki budur.
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start.AddDays(3), start.AddDays(6)));
        await database.SaveChangesAsync();

        var count = await database.Reservations.IgnoreQueryFilters()
            .CountAsync(reservation => reservation.RoomId == scenario.RoomAId);
        count.Should().Be(2, "ardisik konaklamalar cakisma DEGILDIR");
    }

    [RequiresPostgresTheory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.NoShow)]
    public async Task Cancelled_and_no_show_stays_are_outside_the_partial_constraint(
        ReservationStatus status)
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3), status));
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3)));

        // Kisit predikati AvailabilityQuery.IsBlocking ile AYNI kumeyi kullanir: iptal edilen ve
        // gelmeyen misafirin odasi tekrar satilabilir olmalidir.
        await database.SaveChangesAsync();

        var count = await database.Reservations.IgnoreQueryFilters()
            .CountAsync(reservation => reservation.RoomId == scenario.RoomAId);
        count.Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task Soft_deleted_stays_are_outside_the_partial_constraint()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3),
            isDeleted: true));
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3)));

        // Silinmis satir odayi bloke etmemelidir: global query filter onu zaten gormez, kisit da
        // gormemelidir. Aksi halde silinen bir rezervasyon odayi sonsuza dek kilitlerdi.
        await database.SaveChangesAsync();

        var live = await database.Reservations.IgnoreQueryFilters()
            .CountAsync(reservation => reservation.RoomId == scenario.RoomAId && !reservation.IsDeleted);
        live.Should().Be(1);
    }

    [RequiresPostgresFact]
    public async Task Reviving_a_cancelled_stay_onto_an_occupied_range_is_rejected()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        var cancelled = PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3),
            ReservationStatus.Cancelled);

        database.Reservations.Add(cancelled);
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3)));
        await database.SaveChangesAsync();

        // Kisit UPDATE'te de calisir: iptali geri almak satiri kisitin kapsamina sokar.
        cancelled.Status = ReservationStatus.Confirmed;
        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task Different_rooms_may_hold_the_same_dates()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(3)));
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.SecondRoomAId, scenario.GuestAId, start, start.AddDays(3)));

        // Pozitif kontrol: kisit fazla genis degil, anahtar RoomId bazindadir.
        await database.SaveChangesAsync();

        var count = await database.Reservations.IgnoreQueryFilters()
            .CountAsync(reservation => reservation.HotelId == scenario.HotelAId);
        count.Should().Be(2);
    }

    [RequiresPostgresFact]
    public async Task A_zero_night_stay_is_rejected_so_it_cannot_escape_the_exclusion_constraint()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(10);

        await using var database = fixture.CreateDbContext();
        database.Reservations.Add(PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start));

        // CK_Reservations_ValidStay olmasaydi daterange(start, start, '[)') BOS aralik uretirdi;
        // bos aralik hicbir seyle cakismaz, yani boyle bir satir dislama kisitindan SESSIZCE
        // kacar ve odayi istedigi kadar cogaltabilirdi.
        var act = async () => await database.SaveChangesAsync();

        // CHECK ihlali 23514'tur; AppDbContext yalnizca 23505/23P01'i cevirir, digerleri
        // DbUpdateException olarak yukselir.
        await act.Should().ThrowAsync<DbUpdateException>();

        database.ChangeTracker.Clear();
    }

    /// <summary>
    /// <b>Yaris testi:</b> son oda icin N paralel yazma → <b>tam 1</b> basari, N−1 × 409.
    /// <para>
    /// Her yazma <b>ayri bir DbContext</b> (dolayisiyla ayri baglanti ve ayri transaction)
    /// kullanir; tek context ile bu yaris kurulamazdi cunku change tracker islemleri sirayla
    /// yurutur. Kisit olmadan bu test N basari uretirdi — yani bugunku acigin birebir kanitidir.
    /// </para>
    /// </summary>
    [RequiresPostgresFact]
    public async Task Concurrent_writes_for_the_last_room_produce_exactly_one_winner()
    {
        const int Attempts = 8;

        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(30);

        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable.Range(0, Attempts).Select(async _ =>
        {
            await using var database = fixture.CreateDbContext();
            database.Reservations.Add(PublicChannelData.Reservation(
                scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(2)));

            // Tum yazicilar ayni anda serbest birakilir; boylece hepsi gercekten yarisir.
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

        results.Count(succeeded => succeeded).Should().Be(
            1,
            "ayni oda ve ayni tarih icin yalnizca BIR yazma kazanmalidir");

        await using var verification = fixture.CreateDbContext();
        var persisted = await verification.Reservations.IgnoreQueryFilters()
            .CountAsync(reservation => reservation.RoomId == scenario.RoomAId);

        persisted.Should().Be(1, "veritabaninda tek bir konaklama kalmalidir");
    }
}
