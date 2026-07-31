using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// Public kanal semasinin cok kiracililik ve benzersizlik davranislari.
///
/// <para><b>Neden tenant izolasyonu burada test ediliyor:</b> <c>BookingHold</c> ve
/// <c>PublicBooking</c> <c>ITenantEntity</c> uygular, yani global query filter'in bu tipleri
/// <b>otomatik</b> kapsamasi gerekir. Filtre reflection ile uygulandigi icin yeni bir entity
/// eklerken sessizce disarida kalmasi mumkun degildir — ama bu testler onu <i>kanitlar</i>;
/// aksi halde bir otelin misafiri baska otelin rezervasyonunu gorebilirdi.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicChannelSchemaTests(PostgresFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Booking_holds_are_invisible_from_another_hotels_context()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(15);

        await using (var seed = fixture.CreateDbContext())
        {
            seed.BookingHolds.Add(PublicChannelData.Hold(
                scenario.HotelAId, scenario.RoomTypeAId, scenario.RoomAId, start, start.AddDays(2)));
            await seed.SaveChangesAsync();
        }

        await using var hotelA = scenario.CreateApplicationGraph(scenario.HotelAId);
        await using var hotelB = scenario.CreateApplicationGraph(scenario.HotelBId);

        (await hotelA.Database.BookingHolds.CountAsync())
            .Should().Be(1, "hold kendi otelinin baglamindan gorunur");

        (await hotelB.Database.BookingHolds.CountAsync())
            .Should().Be(0, "baska otelin hold'u global query filter tarafindan suzulmelidir");
    }

    [RequiresPostgresFact]
    public async Task Public_bookings_are_invisible_from_another_hotels_context()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(15);
        var reference = NewReference();

        await using (var seed = fixture.CreateDbContext())
        {
            var reservation = PublicChannelData.Reservation(
                scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(2));
            seed.Reservations.Add(reservation);
            seed.PublicBookings.Add(PublicChannelData.Booking(
                scenario.HotelAId, reservation.Id, reference, NewHash()));
            await seed.SaveChangesAsync();
        }

        await using var hotelA = scenario.CreateApplicationGraph(scenario.HotelAId);
        await using var hotelB = scenario.CreateApplicationGraph(scenario.HotelBId);

        (await hotelA.Database.PublicBookings.AnyAsync(b => b.BookingReference == reference))
            .Should().BeTrue();

        // Sozlesme geregi baska otelin token'i/referansi ile erisim 404 uretmelidir; bunun
        // veritabani tarafindaki karsiligi satirin HIC GORUNMEMESIDIR — handler'in ayrica bir
        // otel kontrolu yazmasina gerek kalmaz.
        (await hotelB.Database.PublicBookings.AnyAsync(b => b.BookingReference == reference))
            .Should().BeFalse();
    }

    [RequiresPostgresFact]
    public async Task Room_type_images_are_invisible_from_another_hotels_context()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        await using (var seed = fixture.CreateDbContext())
        {
            seed.RoomTypeImages.Add(new RoomTypeImage
            {
                HotelId = scenario.HotelAId,
                RoomTypeId = scenario.RoomTypeAId,
                Url = $"/assets/it/{Guid.NewGuid():N}.jpg",
                SortOrder = 0,
                AltText = "Doppelzimmer"
            });
            await seed.SaveChangesAsync();
        }

        await using var hotelB = scenario.CreateApplicationGraph(scenario.HotelBId);

        (await hotelB.Database.RoomTypeImages.CountAsync()).Should().Be(0);
    }

    [RequiresPostgresFact]
    public async Task An_access_token_hash_cannot_be_reused_while_the_booking_is_live()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(15);
        var hash = NewHash();

        await using var database = fixture.CreateDbContext();

        var first = PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(2));
        var second = PublicChannelData.Reservation(
            scenario.HotelAId, scenario.SecondRoomAId, scenario.GuestAId, start, start.AddDays(2));
        database.Reservations.AddRange(first, second);
        database.PublicBookings.Add(PublicChannelData.Booking(
            scenario.HotelAId, first.Id, NewReference(), hash));
        await database.SaveChangesAsync();

        database.PublicBookings.Add(PublicChannelData.Booking(
            scenario.HotelAId, second.Id, NewReference(), hash));
        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();
    }

    /// <summary>
    /// Kismi tekil index'in soft-delete davranisi: silinmis bir kayit dogal anahtari
    /// <b>serbest birakir</b>. Bu, projedeki <c>WHERE NOT "IsDeleted"</c> deseninin ta kendisidir
    /// ve filtresiz bir index olsaydi kullanici 409 yerine 500 alirdi.
    /// </summary>
    [RequiresPostgresFact]
    public async Task A_booking_reference_becomes_free_again_after_the_row_is_soft_deleted()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var start = scenario.Today.AddDays(15);
        var reference = NewReference();

        await using var database = fixture.CreateDbContext();

        var first = PublicChannelData.Reservation(
            scenario.HotelAId, scenario.RoomAId, scenario.GuestAId, start, start.AddDays(2));
        var second = PublicChannelData.Reservation(
            scenario.HotelAId, scenario.SecondRoomAId, scenario.GuestAId, start, start.AddDays(2));
        database.Reservations.AddRange(first, second);

        database.PublicBookings.Add(PublicChannelData.Booking(
            scenario.HotelAId, first.Id, reference, NewHash(), isDeleted: true));
        database.PublicBookings.Add(PublicChannelData.Booking(
            scenario.HotelAId, second.Id, reference, NewHash()));

        await database.SaveChangesAsync();

        var live = await database.PublicBookings.IgnoreQueryFilters()
            .CountAsync(booking => booking.BookingReference == reference && !booking.IsDeleted);
        live.Should().Be(1);
    }

    [RequiresPostgresFact]
    public async Task A_hotel_slug_is_globally_unique_among_live_rows()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);
        var slug = $"it-{Guid.NewGuid():N}"[..20];

        await using var database = fixture.CreateDbContext();

        var hotelA = await database.Hotels.IgnoreQueryFilters().FirstAsync(h => h.Id == scenario.HotelAId);
        var hotelB = await database.Hotels.IgnoreQueryFilters().FirstAsync(h => h.Id == scenario.HotelBId);

        hotelA.PublicSlug = slug;
        await database.SaveChangesAsync();

        // Slug GLOBAL benzersizdir (Head Office bazinda degil): URL uzayi globaldir ve iki otel
        // ayni adreste yasayamaz. Ayni Head Office'te olmalari kurali daha da gorunur kilar.
        hotelB.PublicSlug = slug;
        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task A_hotel_slug_must_be_url_safe()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        await using var database = fixture.CreateDbContext();
        var hotel = await database.Hotels.IgnoreQueryFilters().FirstAsync(h => h.Id == scenario.HotelAId);

        // Buyuk harfli bir slug, misafir sitesinde sessizce 404 uretirdi (yol kucuk harf arar).
        hotel.PublicSlug = "Berlin-Mitte";
        var act = async () => await database.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();

        database.ChangeTracker.Clear();
    }

    [RequiresPostgresFact]
    public async Task A_legal_document_version_is_unique_per_hotel_key_and_culture()
    {
        await using var scenario = await BookingScenario.StartAsync(fixture);

        await using var database = fixture.CreateDbContext();
        database.HotelLegalDocuments.Add(Document(scenario.HotelAId, "2026-07-01"));
        await database.SaveChangesAsync();

        // Ayni versiyon iki kez yayimlanamaz: "hangi metin onaylandi" sorusunun tek cevabi olmali.
        database.HotelLegalDocuments.Add(Document(scenario.HotelAId, "2026-07-01"));
        var act = async () => await database.SaveChangesAsync();
        await act.Should().ThrowAsync<ConflictException>();

        database.ChangeTracker.Clear();

        // Yeni bir versiyon serbesttir; eskisi kanit olarak durur.
        database.HotelLegalDocuments.Add(Document(scenario.HotelAId, "2026-09-01"));
        await database.SaveChangesAsync();

        var versions = await database.HotelLegalDocuments.IgnoreQueryFilters()
            .CountAsync(document => document.HotelId == scenario.HotelAId);
        versions.Should().Be(2);
    }

    private static HotelLegalDocument Document(Guid hotelId, string version) => new()
    {
        HotelId = hotelId,
        Key = "terms",
        Culture = "de",
        Version = version,
        Title = "Allgemeine Geschäftsbedingungen",
        BodyHtml = "<p>Demo</p>",
        IsActive = true
    };

    private static string NewReference() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private static string NewHash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
}
