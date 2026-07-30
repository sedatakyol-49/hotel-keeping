using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Settings;

/// <summary>
/// Ayarlar modulunun <b>RBAC</b> testleri (architecture.md §7): policy adi = izin anahtaridir,
/// bu yuzden bir izni token'daki <c>perm</c> claim listesinden CIKARMAK ilgili ucun 403
/// dondurmesini gerektirir. Token'siz istek 401'dir.
/// <para>
/// Sozlesme uc noktalari iki farkli izne baglar: okuma <c>Hotels.View</c>, yazma
/// <c>Settings.Manage</c>. Testler bunlarin <b>birbirinin yerine gecmedigini</b> de dogrular:
/// <c>Hotels.Manage</c> tasiyan bir token bile ayar yazamaz.
/// </para>
/// <para>
/// Her negatif testin yaninda <b>pozitif kontrol</b> vardir: ayni istek dogru izinle 2xx doner.
/// Boylece 403'un gercekten yetkilendirmeden geldigi (yolun yanlis yazilmasi, govdenin gecersiz
/// olmasi vb. degil) kanitlanir.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SettingsAuthorizationTests(PostgresFixture fixture)
{
    private static readonly string[] HotelsViewOnly = [Permissions.HotelsView];

    /// <summary><c>Hotels.Manage</c> dahil ama <c>Settings.Manage</c> HARIC izin kumesi.</summary>
    private static readonly string[] HotelsViewAndManage =
        [Permissions.HotelsView, Permissions.HotelsManage];

    private static readonly string[] SettingsManageOnly = [Permissions.SettingsManage];

    private static readonly string[] EmployeePermissions =
        [Permissions.EmployeesView, Permissions.EmployeesEdit];

    private static Uri Hotels { get; } = new("api/v1/hotels", UriKind.Relative);

    private static Uri HeadOfficeSettings { get; } = new("api/v1/head-office/settings", UriKind.Relative);

    private static Uri SettingsOf(Guid hotelId) =>
        new($"api/v1/hotels/{hotelId}/settings", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Listing_hotels_without_a_token_is_rejected_with_401()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = scenario.CreateAnonymousClient();

        using var response = await client.GetAsync(Hotels);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresPostgresFact]
    public async Task Head_office_settings_without_a_token_is_rejected_with_401()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = scenario.CreateAnonymousClient();

        using var response = await client.GetAsync(HeadOfficeSettings);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresPostgresFact]
    public async Task Settings_update_without_a_token_is_rejected_with_401_before_validation()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = scenario.CreateAnonymousClient();

        // Govde bilincli olarak gecersiz: kimlik dogrulama dogrulamadan ONCE calismalidir.
        using var response = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            new { name = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresPostgresFact]
    public async Task Listing_hotels_without_Hotels_View_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // Ayar yoneticisi izni otel listesini okumaya YETMEZ.
        using var client = await scenario.CreateClientAsync(SettingsManageOnly);

        using var response = await client.GetAsync(Hotels);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_hotels_with_Hotels_View_succeeds()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(HotelsViewOnly);

        using var response = await client.GetAsync(Hotels);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Reading_a_hotel_without_Hotels_View_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(EmployeePermissions);

        using var response = await client.GetAsync(
            new Uri($"api/v1/hotels/{scenario.HotelAId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Updating_hotel_settings_without_Settings_Manage_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // Hotels.View + Hotels.Manage tasiniyor ama ayar yazma ucu Settings.Manage ister.
        using var client = await scenario.CreateClientAsync(HotelsViewAndManage);

        using var response = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            SettingsPayload($"IT Hotel A umbenannt {scenario.Suffix}"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.FindHotelAsync(scenario.HotelAId))!.Name.Should().Be(
            $"IT Hotel A {scenario.Suffix}",
            "403 ile reddedilen istek veriyi degistirmemelidir");
    }

    [RequiresPostgresFact]
    public async Task Updating_hotel_settings_with_Settings_Manage_succeeds()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsManageOnly);

        using var response = await client.PutAsJsonAsync(
            SettingsOf(scenario.HotelAId),
            SettingsPayload($"IT Hotel A umbenannt {scenario.Suffix}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.FindHotelAsync(scenario.HotelAId))!.Name.Should().Be(
            $"IT Hotel A umbenannt {scenario.Suffix}");
    }

    [RequiresPostgresFact]
    public async Task Reading_head_office_settings_without_Settings_Manage_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(HotelsViewAndManage);

        using var response = await client.GetAsync(HeadOfficeSettings);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Reading_head_office_settings_with_Settings_Manage_succeeds()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsManageOnly);

        using var response = await client.GetAsync(HeadOfficeSettings);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Updating_head_office_settings_without_Settings_Manage_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(HotelsViewAndManage);

        using var response = await client.PutAsJsonAsync(
            HeadOfficeSettings,
            new { brandName = $"Gekapert {scenario.Suffix}", defaultCulture = "en" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.FindHeadOfficeAsync(scenario.HeadOfficeId))!.BrandName.Should().Be(
            $"IT Marka A {scenario.Suffix}");
    }

    [RequiresPostgresFact]
    public async Task Updating_head_office_settings_with_Settings_Manage_succeeds()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsManageOnly);

        using var response = await client.PutAsJsonAsync(
            HeadOfficeSettings,
            new { brandName = $"IT Marka A neu {scenario.Suffix}", defaultCulture = "en" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = (await scenario.FindHeadOfficeAsync(scenario.HeadOfficeId))!;
        stored.BrandName.Should().Be($"IT Marka A neu {scenario.Suffix}");
        stored.DefaultCulture.Should().Be("en");
    }

    /// <summary>Gecerli bir ayar govdesi; yalnizca otel adi testten teste degisir.</summary>
    private static object SettingsPayload(string name) => new
    {
        name,
        country = "DE",
        city = "Berlin",
        addressLine = (string?)null,
        postalCode = (string?)null,
        phone = (string?)null,
        email = (string?)null,
        taxNumber = (string?)null,
        defaultCulture = "de",
        currency = "EUR",
        taxProfile = new
        {
            vatRate = 19m,
            reducedVatRate = 7m,
            cityTaxPerPersonNight = 0m,
            cityTaxEnabled = false
        }
    };
}
