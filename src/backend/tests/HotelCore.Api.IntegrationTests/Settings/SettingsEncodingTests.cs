using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Features.HeadOffices.Common;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Settings;

/// <summary>
/// UTF-8 <b>regresyon</b> testleri.
/// <para>
/// <b>Neden kalici bir test:</b> Almanca (<c>ß</c>, <c>ö</c>, <c>ü</c>) ve Turkce
/// (<c>ı</c>, <c>ş</c>, <c>ğ</c>) karakterler elle <c>curl</c> ile denenirken bozuk gorunmustu.
/// Bunun tipik nedeni <b>terminalin</b> kod sayfasidir (Windows konsolu varsayilan olarak
/// cp1252/cp857 kullanir), fakat gercek bir cift-kodlama (mojibake: <c>Musterstraße</c> →
/// <c>MusterstraÃŸe</c>) veya kolon collation'i sorunu da ayni belirtiyi verir. Elle denemek bu
/// ikisini ayirt edemez; asagidaki testler ayirt eder ve hatanin geri gelmesini engeller.
/// </para>
/// <para>
/// Dogrulama <b>uc katmanda</b> yapilir:
/// <list type="number">
///   <item>yazma yanitinin <b>ham baytlari</b> — <c>ß</c> icin <c>0xC3 0x9F</c> dizisi bulunur,
///         mojibake'nin urettigi <c>0xC3 0x83</c> bulunmaz,</item>
///   <item>ayri bir <c>GET</c> istegi — deger veritabanindan tekrar okundugunda da bozulmaz,</item>
///   <item>dogrudan veritabani satiri — API katmani atlanarak saklanan metin karsilastirilir.</item>
/// </list>
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SettingsEncodingTests(PostgresFixture fixture)
{
    private const string GermanName = "Hotel Größe";
    private const string GermanCity = "Zürich";
    private const string MixedAddress = "Musterstraße 12 – Oturma odalı süit";
    private const string TurkishTaxNumber = "TR-Şirket-1";

    /// <summary>Cift kodlanmis (mojibake) <c>ß</c>: <c>Ã</c> + <c>Ÿ</c>.</summary>
    private const string MojibakeMarker = "Ã";

    private static readonly string[] SettingsPermissions =
        [Permissions.HotelsView, Permissions.SettingsManage];

    private static Uri Hotels { get; } = new("api/v1/hotels", UriKind.Relative);

    private static Uri HeadOfficeSettings { get; } =
        new("api/v1/head-office/settings", UriKind.Relative);

    private static Uri SettingsOf(Guid hotelId) =>
        new($"api/v1/hotels/{hotelId}/settings", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task German_and_Turkish_text_survives_a_hotel_settings_round_trip()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        var name = $"{GermanName} {scenario.Suffix}";

        using var written = await client.PutAsJsonAsync(SettingsOf(scenario.HotelAId), new
        {
            name,
            country = "CH",
            city = GermanCity,
            addressLine = MixedAddress,
            postalCode = "8001",
            phone = "+41 44 123",
            email = (string?)null,
            taxNumber = TurkishTaxNumber,
            defaultCulture = "de",
            currency = "CHF",
            taxProfile = new { vatRate = 7.7m, reducedVatRate = 3.7m }
        });

        written.StatusCode.Should().Be(HttpStatusCode.OK);

        // (1) Yazma yanitinin ham baytlari: gercekten UTF-8 mi, cift kodlanmis mi?
        var rawBytes = await written.Content.ReadAsByteArrayAsync();
        var decoded = Encoding.UTF8.GetString(rawBytes);

        decoded.Should().Contain(name).And.Contain(GermanCity).And.Contain(MixedAddress);
        decoded.Should().NotContain(
            MojibakeMarker,
            "cift kodlama (mojibake) olsaydi 'ß' yerine 'Ã' + ikinci bir karakter gorunurdu");

        // Baytlari Latin-1 olarak okumak kodlamayi dogrudan gorunur kilar: Almanca 'sz' harfi
        // (U+00DF) UTF-8'de
        // 0xC3 0x9F'tir, yani Latin-1 goruntusunde "Ã" + "" olarak YAN YANA durur.
        Encoding.Latin1.GetString(rawBytes).Should().Contain(
            "Ã",
            "'ß' govdede UTF-8 olarak (0xC3 0x9F) tasinmalidir");

        // (2) Ayri bir GET: deger veritabanindan tekrar okundugunda da ayni.
        var reread = await client.GetFromJsonAsync<HotelResponse>(
            new Uri($"api/v1/hotels/{scenario.HotelAId}", UriKind.Relative));

        reread!.Name.Should().Be(name);
        reread.City.Should().Be(GermanCity);
        reread.AddressLine.Should().Be(MixedAddress);
        reread.TaxNumber.Should().Be(TurkishTaxNumber);

        // (3) API katmani atlanarak dogrudan veritabani satiri.
        var stored = (await scenario.FindHotelAsync(scenario.HotelAId))!;
        stored.Name.Should().Be(name);
        stored.City.Should().Be(GermanCity);
        stored.AddressLine.Should().Be(MixedAddress);
        stored.TaxNumber.Should().Be(TurkishTaxNumber);
    }

    [RequiresPostgresFact]
    public async Task Non_ascii_text_is_intact_in_the_hotel_list_as_well()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);
        var name = $"{GermanName} {scenario.Suffix}";

        using var written = await client.PutAsJsonAsync(SettingsOf(scenario.HotelAId), new
        {
            name,
            country = "CH",
            city = GermanCity,
            defaultCulture = "de",
            currency = "CHF",
            taxProfile = new { vatRate = 7.7m, reducedVatRate = 3.7m }
        });

        written.StatusCode.Should().Be(HttpStatusCode.OK);

        var hotels = await client.GetFromJsonAsync<IReadOnlyList<HotelListItemResponse>>(Hotels);

        hotels!.Should().ContainSingle().Which.Name.Should().Be(name);
        hotels[0].City.Should().Be(GermanCity);
    }

    [RequiresPostgresFact]
    public async Task Non_ascii_brand_name_survives_a_head_office_settings_round_trip()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync([Permissions.SettingsManage]);

        // Marka adi musteriye gorunur ve koda hardcode edilmez; bozulmasi kabul edilemez.
        var brandName = $"Grüße & Größe Şirketi {scenario.Suffix}";

        using var written = await client.PutAsJsonAsync(
            HeadOfficeSettings,
            new { brandName, defaultCulture = "tr" });

        written.StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await client.GetFromJsonAsync<HeadOfficeSettingsResponse>(HeadOfficeSettings);
        reread!.BrandName.Should().Be(brandName);

        (await scenario.FindHeadOfficeAsync(scenario.HeadOfficeId))!.BrandName
            .Should().Be(brandName);
    }

    [RequiresPostgresFact]
    public async Task Response_bodies_declare_utf8_so_clients_do_not_have_to_guess()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.GetAsync(Hotels);

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // ASP.NET Core JSON yaniti icin charset ya yoktur (JSON varsayilani UTF-8'dir) ya da
        // utf-8 olmalidir; baska bir kod sayfasi istemcide mojibake'ye yol acardi.
        var charSet = response.Content.Headers.ContentType?.CharSet;
        if (charSet is not null)
        {
            charSet.Should().BeEquivalentTo("utf-8");
        }
    }
}
