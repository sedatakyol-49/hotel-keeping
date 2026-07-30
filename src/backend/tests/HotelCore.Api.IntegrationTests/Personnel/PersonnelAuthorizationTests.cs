using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Domain.Common;

namespace HotelCore.Api.IntegrationTests.Personnel;

/// <summary>
/// Personel modulunun <b>RBAC</b> testleri (architecture.md §7): policy adi = izin anahtaridir,
/// bu yuzden bir izni token'daki <c>perm</c> claim listesinden CIKARMAK ilgili ucun 403
/// dondurmesini gerektirir. Token'siz istek 401'dir.
/// <para>
/// Okuma <c>Employees.View</c>, yazma <c>Employees.Edit</c> ister; testler ikisinin birbirinin
/// yerine gecmedigini de dogrular (yalnizca <c>Employees.Edit</c> tasiyan token liste ucunu
/// acamaz).
/// </para>
/// <para>
/// Her negatif testin yaninda <b>pozitif kontrol</b> vardir: ayni istek dogru izinle 2xx doner.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PersonnelAuthorizationTests(PostgresFixture fixture)
{
    private static readonly string[] EmployeesViewOnly = [Permissions.EmployeesView];

    private static readonly string[] EmployeesEditOnly = [Permissions.EmployeesEdit];

    private static readonly string[] EmployeesViewAndEdit =
        [Permissions.EmployeesView, Permissions.EmployeesEdit];

    /// <summary>Personel modulune tamamen yabanci bir izin kumesi.</summary>
    private static readonly string[] UnrelatedPermissions =
        [Permissions.RoomsView, Permissions.HousekeepingView];

    private static Uri Employees { get; } = new("api/v1/employees", UriKind.Relative);

    private static Uri Departments { get; } = new("api/v1/departments", UriKind.Relative);

    private static Uri DepartmentOf(Guid departmentId) =>
        new($"api/v1/departments/{departmentId}", UriKind.Relative);

    private static Uri EmployeeOf(Guid employeeId) =>
        new($"api/v1/employees/{employeeId}", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Listing_employees_without_a_token_is_rejected_with_401()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = scenario.CreateAnonymousClient();

        using var response = await client.GetAsync(Employees);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresPostgresFact]
    public async Task Creating_an_employee_without_a_token_is_rejected_with_401_before_validation()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = scenario.CreateAnonymousClient();

        // Govde bilincli olarak gecersiz: kimlik dogrulama dogrulamadan ONCE calismalidir.
        using var response = await client.PostAsJsonAsync(Employees, new { firstName = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresPostgresFact]
    public async Task Listing_employees_without_Employees_View_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(UnrelatedPermissions);

        using var response = await client.GetAsync(Employees);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_employees_with_only_Employees_Edit_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // Yazma izni okuma iznini kapsamaz: iki izin bagimsizdir.
        using var client = await scenario.CreateClientAsync(EmployeesEditOnly);

        using var response = await client.GetAsync(Employees);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_employees_with_Employees_View_succeeds()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.GetAsync(Employees);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Reading_a_single_employee_without_Employees_View_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeId = await scenario.AddEmployeeAsync(
            scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        using var client = await scenario.CreateClientAsync(EmployeesEditOnly);

        using var response = await client.GetAsync(EmployeeOf(employeeId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_departments_without_Employees_View_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(UnrelatedPermissions);

        using var response = await client.GetAsync(Departments);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Listing_departments_with_Employees_View_succeeds()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.GetAsync(Departments);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresPostgresFact]
    public async Task Creating_an_employee_without_Employees_Edit_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // Yalnizca okuma izni: personeli gorebilir ama kadroyu degistiremez.
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.PostAsJsonAsync(
            Employees,
            EmployeePayload(scenario.DepartmentAId, "Anna", "Becker"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Creating_an_employee_with_Employees_Edit_succeeds_with_201_and_a_location_header()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(EmployeesViewAndEdit);

        using var response = await client.PostAsJsonAsync(
            Employees,
            EmployeePayload(scenario.DepartmentAId, "Anna", "Becker"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [RequiresPostgresFact]
    public async Task Updating_an_employee_without_Employees_Edit_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeId = await scenario.AddEmployeeAsync(
            scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.PutAsJsonAsync(
            EmployeeOf(employeeId),
            EmployeePayload(scenario.DepartmentAId, "Uebernommen", "Becker"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.FindEmployeeIncludingDeletedAsync(employeeId))!.FirstName.Should().Be(
            "Anna",
            "403 ile reddedilen istek veriyi degistirmemelidir");
    }

    [RequiresPostgresFact]
    public async Task Deleting_an_employee_without_Employees_Edit_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeId = await scenario.AddEmployeeAsync(
            scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.DeleteAsync(EmployeeOf(employeeId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.FindEmployeeIncludingDeletedAsync(employeeId))!.IsDeleted.Should().BeFalse();
    }

    [RequiresPostgresFact]
    public async Task Creating_a_department_without_Employees_Edit_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.PostAsJsonAsync(
            Departments,
            new { name = "Kueche", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_department_with_Employees_Edit_succeeds_with_201_and_a_location_header()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(EmployeesViewAndEdit);

        using var response = await client.PostAsJsonAsync(
            Departments,
            new { name = "Kueche", description = "Restaurant" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [RequiresPostgresFact]
    public async Task Deleting_a_department_without_Employees_Edit_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var departmentId = await scenario.AddDepartmentAsync(scenario.HotelAId, "Technik");
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.DeleteAsync(DepartmentOf(departmentId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.FindDepartmentAsync(departmentId)).Should().NotBeNull(
            "403 ile reddedilen istek satiri silmemelidir");
    }

    [RequiresPostgresFact]
    public async Task Deleting_a_department_with_Employees_Edit_succeeds_with_204()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var departmentId = await scenario.AddDepartmentAsync(scenario.HotelAId, "Technik");
        using var client = await scenario.CreateClientAsync(EmployeesViewAndEdit);

        using var response = await client.DeleteAsync(DepartmentOf(departmentId));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await scenario.FindDepartmentAsync(departmentId)).Should().BeNull(
            "departman soft-delete EDILEMEZ; silme gercek silmedir");
    }

    [RequiresPostgresFact]
    public async Task Updating_a_department_without_Employees_Edit_is_rejected_with_403()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(EmployeesViewOnly);

        using var response = await client.PutAsJsonAsync(
            DepartmentOf(scenario.DepartmentAId),
            new { name = "Rezeption umbenannt", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await scenario.FindDepartmentAsync(scenario.DepartmentAId))!.Name.Should().Be("Rezeption");
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
