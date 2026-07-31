using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Entities;
using HotelCore.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Api.IntegrationTests.PublicChannel;

/// <summary>
/// Demo seed'inin misafir sitesini gercekten besleyebildigini dogrular.
///
/// <para><b>Neden gerekli:</b> seed yalnizca uygulama acilisinda (Development) calisir, yani
/// hatalari ancak bir gelistirici uygulamayi baslattiginda ve genellikle <b>yigin izi</b> olarak
/// gorunur. Bu testler yazilirken seed'de iki gercek hata bulundu:
/// (1) <c>PublishedAt</c> icin UTC olmayan bir offset (Npgsql yalnizca offset 0 yazar),
/// (2) fiyat plani seed'inin ada gore idempotent olmasi — mevcut ve <i>baska adli</i> ama ayni
/// araligi kaplayan bir plan <c>EX_RatePlans_NoOverlappingActivePlans</c> kisitini ihlal
/// ediyordu. Ikisi de yalnizca gercek PostgreSQL'de goruluyor.</para>
///
/// <para><b>Idempotentlik:</b> seed ayni veritabaninda iki kez kosturulur; ikinci kosu hicbir
/// satiri cogaltmamali ve hicbir kisiti ihlal etmemelidir.</para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DemoSeedTests(PostgresFixture fixture)
{
    private const string HotelSlug = "berlin-mitte";

    [RequiresPostgresFact]
    public async Task Seeding_twice_is_idempotent_and_configures_the_public_channel()
    {
        await fixture.EnsureMigratedAsync();

        await using (var first = fixture.CreateDbContext())
        {
            await DbSeeder.SeedAsync(first, includeDevelopmentData: true);
        }

        await using (var second = fixture.CreateDbContext())
        {
            // Ikinci kosu: cakisma kisitlari ve tekil index'ler bu adimda patlarsa seed
            // idempotent DEGILDIR ve her uygulama acilisinda hata verirdi.
            await DbSeeder.SeedAsync(second, includeDevelopmentData: true);
        }

        await using var database = fixture.CreateDbContext();

        var hotel = await database.Hotels.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.PublicSlug == HotelSlug);

        hotel.TimeZoneId.Should().Be("Europe/Berlin");
        hotel.CheckInFromLocal.Should().Be(new TimeOnly(15, 0));
        hotel.CheckOutUntilLocal.Should().Be(new TimeOnly(11, 0));
        hotel.PublicBookingSettings.IsEnabled.Should().BeTrue("misafir sitesi demo edilebilmelidir");
        hotel.CancellationPolicy.CutoffLocalTime.Should().Be(new TimeOnly(18, 0));

        // Steuernummer ve USt-IdNr. AYRI kolonlardir; demo verisi bu ayrimi gostermelidir.
        hotel.TaxNumber.Should().NotBeNullOrWhiteSpace();
        hotel.VatId.Should().StartWith("DE");
        hotel.VatId.Should().NotBe(hotel.TaxNumber);

        // §5 DDG kunyesi TAMAMEN veritabanindan gelir; hicbir alan koda gomulu degildir.
        hotel.LegalProfile.LegalEntityName.Should().NotBeNullOrWhiteSpace();
        hotel.LegalProfile.RegisterCourt.Should().NotBeNullOrWhiteSpace();
        hotel.LegalProfile.RegisterNumber.Should().NotBeNullOrWhiteSpace();
        hotel.LegalProfile.RepresentedBy.Should().NotBeNullOrWhiteSpace();

        var brandSlug = await database.HeadOffices.AsNoTracking()
            .Where(headOffice => headOffice.Id == hotel.HeadOfficeId)
            .Select(headOffice => headOffice.PublicSlug)
            .SingleAsync();
        brandSlug.Should().NotBeNullOrWhiteSpace("marka sitesi otel listesi slug ile calisir");

        await AssertLegalDocumentsAsync(database, hotel.Id);
        await AssertRoomTypeContentAsync(database, hotel.Id);
        await AssertWebRatePlanExistsAsync(database, hotel.Id);
    }

    private static async Task AssertLegalDocumentsAsync(
        HotelCore.Infrastructure.Persistence.AppDbContext database,
        Guid hotelId)
    {
        var documents = await database.HotelLegalDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(document => document.HotelId == hotelId)
            .ToListAsync();

        // Rezervasyon uc noktasi UC ayri onay versiyonu dogrular (AGB, aydinlatma, cayma
        // bildirimi); ucunun de yayimlanmis bir metni olmalidir, aksi halde
        // LEGAL_TEXT_CHANGED kontrolu dayanaksiz kalir.
        documents.Select(document => document.Key).Distinct()
            .Should().Contain(["terms", "privacy", "withdrawal"]);

        documents.Should().AllSatisfy(document =>
        {
            document.Version.Should().NotBeNullOrWhiteSpace();
            document.BodyHtml.Should().NotBeNullOrWhiteSpace();

            // Sanitizasyon sunucunun sorumlulugudur; seed o kurali ihlal eden bir ornek vermez.
            document.BodyHtml.Should().NotContain("<script", "izinli etiket listesi disinda");
            document.BodyHtml.Should().NotContain("<iframe");
            document.BodyHtml.Should().NotContain("onerror=");
        });

        documents.Where(document => string.Equals(document.Key, "terms", StringComparison.Ordinal))
            .Select(document => document.Culture)
            .Should().Contain("de", "otelin varsayilan dilindeki metin her zaman bulunmalidir");
    }

    private static async Task AssertRoomTypeContentAsync(
        HotelCore.Infrastructure.Persistence.AppDbContext database,
        Guid hotelId)
    {
        // YALNIZCA seed'in sahibi oldugu oda tipleri denetlenir: demo otele gelistiricinin elle
        // ekledigi oda tipleri olabilir ve onlarin icerigi seed'in sorumlulugunda degildir.
        string[] seededCodes = ["SGL", "DBL", "SUI"];

        var roomTypes = await database.RoomTypes.IgnoreQueryFilters().AsNoTracking()
            .Where(roomType => roomType.HotelId == hotelId && seededCodes.Contains(roomType.Code))
            .ToListAsync();

        roomTypes.Should().HaveCount(seededCodes.Length);

        // Varsayilan dildeki metin Almanca olmalidir: misafir sitesi bu metni birebir gosterir.
        roomTypes.Should().AllSatisfy(roomType =>
        {
            roomType.Description.Should().NotBeNullOrWhiteSpace();
            roomType.Amenities.Should().NotBeNullOrWhiteSpace();
        });

        var roomTypeIds = roomTypes.ConvertAll(roomType => roomType.Id);

        var images = await database.RoomTypeImages.IgnoreQueryFilters().AsNoTracking()
            .Where(image => image.HotelId == hotelId && roomTypeIds.Contains(image.RoomTypeId))
            .ToListAsync();

        images.Should().NotBeEmpty("katalog ve detay sayfasi gorselsiz demo edilemez");
        images.Should().AllSatisfy(image =>
        {
            image.AltText.Should().NotBeNullOrWhiteSpace("WCAG 1.1.1: alt metin zorunludur");
            image.Width.Should().NotBeNull("boyutlar CLS'i onlemek icin isaretlemeye yazilir");
            image.Height.Should().NotBeNull();
        });

        var translations = await database.Translations.AsNoTracking()
            .Where(translation => translation.EntityType == nameof(RoomType)
                                  && roomTypeIds.Contains(translation.EntityId))
            .ToListAsync();

        translations.Select(translation => translation.Culture).Distinct()
            .Should().Contain(["en", "tr"], "dinamik icerik cok dillidir");

        // Idempotentlik: ayni (entity, alan, dil) icin tek satir.
        translations
            .GroupBy(translation =>
                (translation.EntityId, translation.Field, translation.Culture))
            .Should().AllSatisfy(group => group.Should().HaveCount(1));
    }

    private static async Task AssertWebRatePlanExistsAsync(
        HotelCore.Infrastructure.Persistence.AppDbContext database,
        Guid hotelId)
    {
        // architecture-public-booking.md §7.1: Channel = Direct planlari WEB'e uygulanmaz.
        // "Tum kanallar" plani olmazsa web fiyati sessizce RoomType.BasePrice'a duserdi.
        var allChannelPlans = await database.RatePlans.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(plan => plan.HotelId == hotelId && plan.Channel == null && plan.IsActive);

        allChannelPlans.Should().BeGreaterThan(
            0,
            "seed web icin gecerli bir fiyat plani icermelidir");
    }
}
