using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Features.HeadOffices.Common;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Settings;

/// <summary>
/// Ayarlar modulunun uctan uca sozlesme davranislari (api-contracts.md → "Hotels &amp; Ayarlar"):
/// normalizasyonun HTTP sinirindan gecerek veritabanina yansimasi, sayaclar, enum adlari ve
/// dogrulama hatalarinin RFC 7807 bicimi.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SettingsContractTests(PostgresFixture fixture)
{
    private static readonly string[] SettingsPermissions =
        [Permissions.HotelsView, Permissions.SettingsManage];

    private static Uri Hotels { get; } = new("api/v1/hotels", UriKind.Relative);

    private static Uri HeadOfficeSettings { get; } =
        new("api/v1/head-office/settings", UriKind.Relative);

    private static Uri HotelOf(Guid hotelId) => new($"api/v1/hotels/{hotelId}", UriKind.Relative);

    private static Uri SettingsOf(Guid hotelId) =>
        new($"api/v1/hotels/{hotelId}/settings", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Hotel_detail_returns_the_tax_profile_and_the_live_room_count()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "101");
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "102");
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        var hotel = await client.GetFromJsonAsync<HotelResponse>(HotelOf(scenario.HotelAId));

        hotel.Should().NotBeNull();
        hotel!.HeadOfficeId.Should().Be(scenario.HeadOfficeId);
        hotel.Country.Should().Be("DE", "ulke enum ADI olarak doner, sayi degil");
        hotel.Currency.Should().Be("EUR");
        hotel.RoomCount.Should().Be(2);
        hotel.TaxProfile.VatRate.Should().Be(19m);
        hotel.TaxProfile.ReducedVatRate.Should().Be(7m);
    }

    [RequiresPostgresFact]
    public async Task Hotel_list_returns_a_plain_array_with_the_selector_fields()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddRoomAsync(scenario.HotelAId, scenario.RoomTypeAId, "101");
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.GetAsync(Hotels);
        var payload = await response.Content.ReadAsStringAsync();
        var hotels = JsonSerializer.Deserialize<IReadOnlyList<HotelListItemResponse>>(
            payload,
            JsonSerializerOptions.Web);

        // Sayfalama YOKTUR: govde duz bir dizidir (otel secici bu listeyi besler).
        payload.TrimStart().Should().StartWith("[");
        hotels.Should().ContainSingle();
        hotels![0].RoomCount.Should().Be(1);
        hotels[0].City.Should().Be("Berlin");
        hotels[0].DefaultCulture.Should().Be("de");
    }

    [RequiresPostgresFact]
    public async Task Settings_update_normalises_the_currency_and_the_culture_end_to_end()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            Payload(scenario, currency: "chf", defaultCulture: "DE-de", country: "CH", city: "Zug"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<HotelResponse>();
        updated!.Currency.Should().Be("CHF");
        updated.DefaultCulture.Should().Be("de");
        updated.Country.Should().Be("CH");

        var stored = (await scenario.FindHotelAsync(scenario.HotelAId))!;
        stored.Currency.Should().Be("CHF", "normalizasyon yaniti degil VERIYI etkiler");
        stored.DefaultCulture.Should().Be("de");
    }

    [RequiresPostgresFact]
    public async Task Settings_update_stores_blank_optional_fields_as_null()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var filled = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            Payload(scenario, addressLine: "Hauptstrasse 1", taxNumber: "DE123456789"));
        using var cleared = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            Payload(scenario, addressLine: "   ", taxNumber: string.Empty));

        filled.StatusCode.Should().Be(HttpStatusCode.OK);
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = (await scenario.FindHotelAsync(scenario.HotelAId))!;
        stored.AddressLine.Should().BeNull("\"\" ile null ayrimi veride tutulmaz");
        stored.TaxNumber.Should().BeNull();
    }

    [RequiresPostgresFact]
    public async Task Settings_update_persists_the_tax_profile()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.PutAsJsonAsync(SettingsOf(scenario.HotelAId), new
        {
            name = $"IT Hotel A {scenario.Suffix}",
            country = "DE",
            city = "Berlin",
            defaultCulture = "de",
            currency = "EUR",
            taxProfile = new
            {
                vatRate = 19m,
                reducedVatRate = 7m,
                cityTaxPerPersonNight = 3.50m,
                cityTaxEnabled = true
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Vergi oranlari koda hardcode edilmez (architecture.md §4.1); faturalama bunlari okur.
        var stored = (await scenario.FindHotelAsync(scenario.HotelAId))!.TaxProfile;
        stored.CityTaxPerPersonNight.Should().Be(3.50m);
        stored.CityTaxEnabled.Should().BeTrue();
    }

    [RequiresPostgresFact]
    public async Task Invalid_currency_returns_400_with_a_pascal_case_error_key()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            Payload(scenario, currency: "EURO"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("Currency", out _)
            .Should().BeTrue("errors anahtarlari PascalCase alan adlaridir");
    }

    [RequiresPostgresFact]
    public async Task Unsupported_default_culture_returns_400()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            Payload(scenario, defaultCulture: "fr"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Unknown_country_returns_400()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            Payload(scenario, country: "XX"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Head_office_settings_report_the_brand_and_its_hotel_count()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync([Permissions.SettingsManage]);

        var settings = await client.GetFromJsonAsync<HeadOfficeSettingsResponse>(HeadOfficeSettings);

        settings.Should().NotBeNull();
        settings!.Id.Should().Be(scenario.HeadOfficeId);
        settings.BrandName.Should().Be($"IT Marka A {scenario.Suffix}");
        settings.DefaultCulture.Should().Be("de");
        settings.HotelCount.Should().Be(2, "yalnizca bu markanin otelleri sayilir");
    }

    [RequiresPostgresFact]
    public async Task Head_office_settings_update_trims_the_brand_name_and_normalises_the_culture()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync([Permissions.SettingsManage]);

        using var response = await client.PutAsJsonAsync(
            HeadOfficeSettings,
            new { brandName = $"  IT Marka A neu {scenario.Suffix}  ", defaultCulture = "TR-tr" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<HeadOfficeSettingsResponse>();
        updated!.BrandName.Should().Be($"IT Marka A neu {scenario.Suffix}");
        updated.DefaultCulture.Should().Be("tr");

        var stored = (await scenario.FindHeadOfficeAsync(scenario.HeadOfficeId))!;
        stored.BrandName.Should().Be($"IT Marka A neu {scenario.Suffix}");
        stored.DefaultCulture.Should().Be("tr");
    }

    [RequiresPostgresFact]
    public async Task Empty_brand_name_returns_400()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync([Permissions.SettingsManage]);

        using var response = await client.PutAsJsonAsync(
            HeadOfficeSettings,
            new { brandName = "   ", defaultCulture = "de" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await scenario.FindHeadOfficeAsync(scenario.HeadOfficeId))!.BrandName.Should().Be(
            $"IT Marka A {scenario.Suffix}");
    }

    [RequiresPostgresFact]
    public async Task Unknown_hotel_id_returns_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions);

        using var response = await client.GetAsync(HotelOf(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Gecerli bir ayar govdesi; testler yalnizca ilgilendikleri alani degistirir. Otel adi
    /// sahnenin sonekini tasir (marka icinde benzersizlik kisiti).
    /// </summary>
    private static object Payload(
        SettingsAndPersonnelScenario scenario,
        string? name = null,
        string country = "DE",
        string city = "Berlin",
        string? addressLine = null,
        string? taxNumber = null,
        string defaultCulture = "de",
        string currency = "EUR") => new
        {
            name = name ?? $"IT Hotel A {scenario.Suffix}",
            country,
            city,
            addressLine,
            postalCode = (string?)null,
            phone = (string?)null,
            email = (string?)null,
            taxNumber,
            defaultCulture,
            currency,
            taxProfile = new
            {
                vatRate = 19m,
                reducedVatRate = 7m,
                cityTaxPerPersonNight = 0m,
                cityTaxEnabled = false
            }
        };
}
