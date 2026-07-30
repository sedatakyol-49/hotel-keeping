using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Api.Services;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Departments.Common;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Application.Features.Hotels.Common;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Personnel;

/// <summary>
/// Personel ve Ayarlar modullerinde multi-tenant izolasyonun uctan uca testi — mimarinin en
/// kritik guvenlik garantisi (architecture.md §3).
/// <para>
/// Sahne: ayni Head Office'e bagli iki otel (A, B) ve ayrica <b>baska bir marka</b>.
/// Kullanicinin token'i yalnizca A otelini tasir ve <c>allHotels</c> false'tur. Beklenen davranis:
/// <list type="bullet">
///   <item>B otelinin calisani <c>GET /employees/{id}</c> ile <b>404</b>'tur — 403 DEGIL: kaydin
///         var oldugu bilgisi bile sizdirilmaz,</item>
///   <item>B'nin departmaniyla calisan olusturma girisimi <b>404</b>'tur,</item>
///   <item><c>X-Hotel-Id: B</c> ile kapsam degistirme girisimi <b>403</b>'tur ve endpoint hic
///         calismaz (<c>HotelContextMiddleware</c>),</item>
///   <item>liste uclari ve <c>totalCount</c> yalnizca aktif otelin satirlarini gorur,</item>
///   <item>erisilemeyen otel <c>GET /hotels/{id}</c> ile <b>404</b>'tur; <c>allHotels</c> yetkisi
///         bile marka sinirini asmaz.</item>
/// </list>
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PersonnelTenantIsolationTests(PostgresFixture fixture)
{
    private static readonly string[] PersonnelPermissions =
        [Permissions.EmployeesView, Permissions.EmployeesEdit];

    private static readonly string[] SettingsPermissions =
        [Permissions.HotelsView, Permissions.SettingsManage];

    private static Uri Employees { get; } = new("api/v1/employees", UriKind.Relative);

    private static Uri Departments { get; } = new("api/v1/departments", UriKind.Relative);

    private static Uri Hotels { get; } = new("api/v1/hotels", UriKind.Relative);

    private static Uri EmployeeOf(Guid employeeId) =>
        new($"api/v1/employees/{employeeId}", UriKind.Relative);

    private static Uri HotelOf(Guid hotelId) => new($"api/v1/hotels/{hotelId}", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Employee_of_another_hotel_is_reported_as_404_not_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeInB = await scenario.AddEmployeeAsync(
            scenario.HotelBId, scenario.DepartmentBId, "Bea", "Bauer");

        // Token yalnizca A otelini tasir.
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        using var response = await client.GetAsync(EmployeeOf(employeeInB));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "baska otelin kaydi 'yok' sayilir; varligi sizdirilmaz");
    }

    [RequiresPostgresFact]
    public async Task Creating_an_employee_with_a_department_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        using var response = await client.PostAsJsonAsync(
            Employees,
            EmployeePayload(scenario.DepartmentBId, "Anna", "Becker"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Moving_an_employee_into_a_department_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeId = await scenario.AddEmployeeAsync(
            scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        using var response = await client.PutAsJsonAsync(
            EmployeeOf(employeeId),
            EmployeePayload(scenario.DepartmentBId, "Anna", "Becker"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await scenario.FindEmployeeIncludingDeletedAsync(employeeId))!.DepartmentId
            .Should().Be(scenario.DepartmentAId);
    }

    [RequiresPostgresFact]
    public async Task Updating_an_employee_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeInB = await scenario.AddEmployeeAsync(
            scenario.HotelBId, scenario.DepartmentBId, "Bea", "Bauer");
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        using var response = await client.PutAsJsonAsync(
            EmployeeOf(employeeInB),
            EmployeePayload(scenario.DepartmentAId, "Uebernommen", "Bauer"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await scenario.FindEmployeeIncludingDeletedAsync(employeeInB))!.FirstName.Should().Be("Bea");
    }

    [RequiresPostgresFact]
    public async Task Deleting_an_employee_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeInB = await scenario.AddEmployeeAsync(
            scenario.HotelBId, scenario.DepartmentBId, "Bea", "Bauer");
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        using var response = await client.DeleteAsync(EmployeeOf(employeeInB));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await scenario.FindEmployeeIncludingDeletedAsync(employeeInB))!.IsDeleted.Should().BeFalse();
    }

    [RequiresPostgresFact]
    public async Task Deleting_a_department_of_another_hotel_is_reported_as_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        using var response = await client.DeleteAsync(
            new Uri($"api/v1/departments/{scenario.DepartmentBId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await scenario.FindDepartmentAsync(scenario.DepartmentBId)).Should().NotBeNull();
    }

    [RequiresPostgresFact]
    public async Task Employee_list_and_total_count_cover_only_the_active_hotel()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        await scenario.AddEmployeeAsync(scenario.HotelBId, scenario.DepartmentBId, "Bea", "Bauer");
        await scenario.AddEmployeeAsync(scenario.HotelBId, scenario.DepartmentBId, "Carl", "Cramer");

        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        var page = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(Employees);

        page.Should().NotBeNull();
        page!.Items.Select(employee => employee.FullName).Should().Equal("Anna Becker");
        page.TotalCount.Should().Be(1, "toplam sayac da tenant filtresine tabidir");
    }

    [RequiresPostgresFact]
    public async Task Department_list_covers_only_the_active_hotel()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);

        var departments = await client.GetFromJsonAsync<IReadOnlyList<DepartmentResponse>>(Departments);

        departments.Should().NotBeNull();
        departments!.Select(department => department.Name).Should().Equal("Rezeption");
    }

    [RequiresPostgresFact]
    public async Task Switching_to_a_hotel_the_user_cannot_access_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddEmployeeAsync(scenario.HotelBId, scenario.DepartmentBId, "Bea", "Bauer");

        // X-Hotel-Id ile B oteline gecme girisimi: erisim listesinde B yok.
        using var client = await scenario.CreateClientAsync(
            PersonnelPermissions,
            [scenario.HotelAId],
            activeHotelId: scenario.HotelBId);

        using var response = await client.GetAsync(Employees);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task User_with_access_to_both_hotels_can_switch_scope_with_the_hotel_header()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        await scenario.AddEmployeeAsync(scenario.HotelBId, scenario.DepartmentBId, "Bea", "Bauer");

        using var inHotelB = await scenario.CreateClientAsync(
            PersonnelPermissions,
            [scenario.HotelAId, scenario.HotelBId],
            activeHotelId: scenario.HotelBId);

        var page = await inHotelB.GetFromJsonAsync<PagedResult<EmployeeResponse>>(Employees);

        page!.Items.Select(employee => employee.FullName).Should().Equal("Bea Bauer");
    }

    [RequiresPostgresFact]
    public async Task Malformed_hotel_header_is_rejected_with_400()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions, [scenario.HotelAId]);
        client.DefaultRequestHeaders.Add(CurrentUser.HotelHeaderName, "not-a-guid");

        using var response = await client.GetAsync(Employees);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Hotel_the_user_cannot_access_is_reported_as_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // Erisim satiri yalnizca A oteli icin yazilir.
        using var client = await scenario.CreateClientAsync(SettingsPermissions, [scenario.HotelAId]);

        using var response = await client.GetAsync(HotelOf(scenario.HotelBId));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "erisilemeyen otel 404 doner; 403 otelin varligini sizdirirdi");
    }

    [RequiresPostgresFact]
    public async Task Hotel_list_contains_only_the_hotels_granted_to_the_user()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions, [scenario.HotelAId]);

        var hotels = await client.GetFromJsonAsync<IReadOnlyList<HotelListItemResponse>>(Hotels);

        hotels.Should().NotBeNull();
        hotels!.Select(hotel => hotel.Id).Should().Equal(scenario.HotelAId);
    }

    [RequiresPostgresFact]
    public async Task Head_office_user_sees_every_hotel_of_the_own_brand_but_no_other_brand()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // allHotels = true ve X-Hotel-Id yok → konsolide okuma; kapsam yine markadir.
        using var client = await scenario.CreateClientAsync(
            SettingsPermissions,
            [],
            canAccessAllHotels: true);

        var hotels = await client.GetFromJsonAsync<IReadOnlyList<HotelListItemResponse>>(Hotels);

        // Yanit govdesinin varligi once dogrulanir: sonraki iki satirda null-forgiving
        // (`!`) kullanmak yerine burada patlarsa hata mesaji anlasilir olur.
        hotels.Should().NotBeNull();

        var hotelIds = hotels.Select(hotel => hotel.Id).ToList();
        hotelIds.Should().BeEquivalentTo([scenario.HotelAId, scenario.HotelBId]);
        hotelIds.Should().NotContain(scenario.OtherBrandHotelId);
    }

    [RequiresPostgresFact]
    public async Task Hotel_of_another_brand_is_reported_as_404_for_a_head_office_user()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(
            SettingsPermissions,
            [],
            canAccessAllHotels: true);

        using var response = await client.GetAsync(HotelOf(scenario.OtherBrandHotelId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [RequiresPostgresFact]
    public async Task Updating_settings_of_a_hotel_the_user_cannot_access_is_reported_as_404()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(SettingsPermissions, [scenario.HotelAId]);

        using var response = await client.PutAsJsonAsync(
            new Uri($"api/v1/hotels/{scenario.HotelBId}/settings", UriKind.Relative),
            new
            {
                name = $"Gekapert {scenario.Suffix}",
                country = "DE",
                city = "Berlin",
                defaultCulture = "de",
                currency = "EUR",
                taxProfile = new { vatRate = 19m, reducedVatRate = 7m }
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await scenario.FindHotelAsync(scenario.HotelBId))!.Name.Should().Be(
            $"IT Hotel B {scenario.Suffix}",
            "reddedilen istek veriyi degistirmemelidir");
    }

    [RequiresPostgresFact]
    public async Task Head_office_settings_of_another_brand_are_unreachable()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // Kullanici marka A'ya baglidir; istek govdesinde Head Office kimligi tasinmaz.
        using var client = await scenario.CreateClientAsync([Permissions.SettingsManage]);

        using var response = await client.PutAsJsonAsync(
            new Uri("api/v1/head-office/settings", UriKind.Relative),
            new { brandName = $"IT Marka A neu {scenario.Suffix}", defaultCulture = "de" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.FindHeadOfficeAsync(scenario.OtherBrandHeadOfficeId))!.BrandName
            .Should().Be(
                $"IT Marka B {scenario.Suffix}",
                "baska markanin ayarlarina erisim yolu hic acilmaz");
    }

    /// <summary>Gecerli bir calisan govdesi.</summary>
    private static object EmployeePayload(Guid departmentId, string firstName, string lastName) => new
    {
        firstName,
        lastName,
        email = (string?)null,
        phone = (string?)null,
        staffNumber = (string?)null,
        departmentId,
        employmentType = "FullTime",
        annualLeaveDays = 28m,
        hiredOn = "2024-03-01",
        terminatedOn = (string?)null
    };
}
