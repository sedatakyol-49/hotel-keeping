using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using HotelCore.Api.IntegrationTests.Infrastructure;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Departments.Common;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Api.IntegrationTests.Personnel;

/// <summary>
/// Personel modulunun gercek PostgreSQL'e karsi dogrulanmasi gereken sozlesme davranislari:
/// benzersizlik ihlalinin <b>409</b>'a cevrilmesi (SQLSTATE 23505), soft-delete sonrasi personel
/// numarasinin yeniden kullanilabilmesi (kismi unique index), <c>lower()</c>'in Almanca
/// umlaut'lardaki davranisi, konsolide modda yazma isteginin reddi ve sayfalama/siralama
/// sozlesmesi.
/// <para>
/// Bu davranislarin bir kismi handler seviyesinde SQLite ile <b>guvenilir sekilde
/// dogrulanamaz</b> (409 cevirisi Npgsql'e, umlaut kucultmesi veritabani collation'ina baglidir);
/// bu yuzden burada yer alirlar.
/// </para>
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PersonnelContractTests(PostgresFixture fixture)
{
    private static readonly string[] PersonnelPermissions =
        [Permissions.EmployeesView, Permissions.EmployeesEdit];

    private static Uri Employees { get; } = new("api/v1/employees", UriKind.Relative);

    private static Uri Departments { get; } = new("api/v1/departments", UriKind.Relative);

    private static Uri EmployeeOf(Guid employeeId) =>
        new($"api/v1/employees/{employeeId}", UriKind.Relative);

    [RequiresPostgresFact]
    public async Task Creating_an_employee_without_the_hotel_header_in_consolidated_mode_returns_400()
    {
        // Head Office kullanicisi X-Hotel-Id gondermezse baglam konsolidedir (HotelId = null) ve
        // kaydin hangi otele yazilacagi belirsizdir: sessizce bir otel secmek yerine 400 doner.
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(
            PersonnelPermissions,
            [],
            canAccessAllHotels: true);

        using var response = await client.PostAsJsonAsync(
            Employees,
            EmployeePayload(scenario.DepartmentAId, "Anna", "Becker"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("X-Hotel-Id", out _)
            .Should().BeTrue("hangi header'in eksik oldugu 'errors' sozlugunde bildirilmelidir");
    }

    [RequiresPostgresFact]
    public async Task Head_office_user_can_create_an_employee_when_the_hotel_header_selects_a_hotel()
    {
        // Yukaridaki 400'un sebebinin izin degil aktif otel belirsizligi oldugunu kanitlar.
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(
            PersonnelPermissions,
            [],
            canAccessAllHotels: true,
            activeHotelId: scenario.HotelAId);

        using var response = await client.PostAsJsonAsync(
            Employees,
            EmployeePayload(scenario.DepartmentAId, "Anna", "Becker"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
        (await scenario.FindEmployeeIncludingDeletedAsync(created!.Id))!.HotelId
            .Should().Be(scenario.HotelAId);
    }

    [RequiresPostgresFact]
    public async Task Creating_a_department_without_the_hotel_header_in_consolidated_mode_returns_400()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(
            PersonnelPermissions,
            [],
            canAccessAllHotels: true);

        using var response = await client.PostAsJsonAsync(Departments, new { name = "Kueche" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("X-Hotel-Id", out _).Should().BeTrue();
    }

    [RequiresPostgresFact]
    public async Task Duplicate_department_name_returns_409_not_500()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        using var first = await client.PostAsJsonAsync(Departments, new { name = "Kueche" });
        using var duplicate = await client.PostAsJsonAsync(Departments, new { name = "Kueche" });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [RequiresPostgresFact]
    public async Task Duplicate_staff_number_returns_409_not_500()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);
        var payload = EmployeePayload(scenario.DepartmentAId, "Anna", "Becker", staffNumber: "P-014");

        using var first = await client.PostAsJsonAsync(Employees, payload);
        using var duplicate = await client.PostAsJsonAsync(
            Employees,
            EmployeePayload(scenario.DepartmentAId, "Ben", "Colin", staffNumber: "P-014"));

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [RequiresPostgresFact]
    public async Task Staff_number_of_a_deleted_employee_can_be_used_again()
    {
        // Kismi unique index (WHERE NOT "IsDeleted") sayesinde: isten cikan personelin numarasi
        // yeniden verilebilir. Index filtresiz olsaydi on kontrol gecer, INSERT 23505 ile
        // patlar ve kullaniciya 409 yerine 500 donerdi.
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);
        var payload = EmployeePayload(scenario.DepartmentAId, "Anna", "Becker", staffNumber: "P-014");

        using var first = await client.PostAsJsonAsync(Employees, payload);
        var firstEmployee = await first.Content.ReadFromJsonAsync<EmployeeResponse>();

        using var deleted = await client.DeleteAsync(EmployeeOf(firstEmployee!.Id));
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var recreated = await client.PostAsJsonAsync(
            Employees,
            EmployeePayload(scenario.DepartmentAId, "Ben", "Colin", staffNumber: "P-014"));

        recreated.StatusCode.Should().Be(HttpStatusCode.Created);
        (await scenario.FindEmployeeIncludingDeletedAsync(firstEmployee.Id))!.IsDeleted
            .Should().BeTrue("eski satir soft-delete edilmis olarak durmalidir");
    }

    [RequiresPostgresFact]
    public async Task Termination_date_before_the_hire_date_returns_400()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        using var response = await client.PostAsJsonAsync(Employees, new
        {
            firstName = "Anna",
            lastName = "Becker",
            departmentId = scenario.DepartmentAId,
            employmentType = "FullTime",
            annualLeaveDays = 28m,
            hiredOn = "2026-01-15",
            terminatedOn = "2026-01-14"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("TerminatedOn", out _)
            .Should().BeTrue("errors anahtarlari PascalCase alan adlaridir");
    }

    [RequiresPostgresFact]
    public async Task Unknown_employment_type_returns_400()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        using var response = await client.PostAsJsonAsync(Employees, new
        {
            firstName = "Anna",
            lastName = "Becker",
            departmentId = scenario.DepartmentAId,
            employmentType = "Praktikant",
            annualLeaveDays = 28m,
            hiredOn = "2024-03-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Numeric_employment_type_is_rejected_because_the_contract_expects_the_enum_name()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        using var response = await client.PostAsJsonAsync(Employees, new
        {
            firstName = "Anna",
            lastName = "Becker",
            departmentId = scenario.DepartmentAId,
            employmentType = 2,
            annualLeaveDays = 28m,
            hiredOn = "2024-03-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [RequiresPostgresFact]
    public async Task Employee_list_exposes_the_paging_contract_and_sorts_by_last_name_then_first_name()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);

        // Ekleme sirasi bilincli olarak karisik: siralama SQL'den gelmelidir.
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Zoe", "Becker");
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Carl", "Adler");
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");

        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        var response = await client.GetAsync(Employees);
        var payload = await response.Content.ReadAsStringAsync();
        var page = JsonSerializer.Deserialize<PagedResult<EmployeeResponse>>(
            payload,
            JsonSerializerOptions.Web);

        // Sozlesme alan adlari (frontend bunlara baglidir).
        payload.Should().Contain("\"items\"").And.Contain("\"page\"")
            .And.Contain("\"pageSize\"").And.Contain("\"totalCount\"");

        page!.Page.Should().Be(1);
        page.PageSize.Should().Be(PageQuery.DefaultPageSize);
        page.TotalCount.Should().Be(3);
        page.Items.Select(employee => employee.FullName).Should().Equal(
            "Carl Adler", "Anna Becker", "Zoe Becker");
    }

    [RequiresPostgresFact]
    public async Task Second_page_returns_the_remaining_employees_with_the_unpaged_total()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        foreach (var lastName in new[] { "Adler", "Becker", "Cramer", "Decker", "Ebert" })
        {
            await scenario.AddEmployeeAsync(
                scenario.HotelAId, scenario.DepartmentAId, "Test", lastName);
        }

        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        var page = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            new Uri("api/v1/employees?page=2&pageSize=2", UriKind.Relative));

        page!.Page.Should().Be(2);
        page.TotalCount.Should().Be(5, "toplam sayac sayfalamadan ONCE hesaplanir");
        page.Items.Select(employee => employee.LastName).Should().Equal("Cramer", "Decker");
    }

    [RequiresPostgresFact]
    public async Task Deleting_a_department_with_employees_returns_409()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        using var response = await client.DeleteAsync(
            new Uri($"api/v1/departments/{scenario.DepartmentAId}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        (await scenario.FindDepartmentAsync(scenario.DepartmentAId)).Should().NotBeNull();
    }

    [RequiresPostgresFact]
    public async Task Deleting_an_employee_soft_deletes_it_and_removes_it_from_the_list()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeId = await scenario.AddEmployeeAsync(
            scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        using var deleted = await client.DeleteAsync(EmployeeOf(employeeId));
        var page = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            new Uri("api/v1/employees?includeTerminated=true", UriKind.Relative));
        using var afterwards = await client.GetAsync(EmployeeOf(employeeId));

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        afterwards.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var stored = await scenario.FindEmployeeIncludingDeletedAsync(employeeId);
        stored.Should().NotBeNull("izin/zaman kayitlari korunur; satir fiziksel olarak SILINMEZ");
        stored!.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().NotBeNull();
    }

    [RequiresPostgresFact]
    public async Task Terminated_employees_are_excluded_unless_include_terminated_is_requested()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        await scenario.AddEmployeeAsync(
            scenario.HotelAId,
            scenario.DepartmentAId,
            "Bea",
            "Ehemalig",
            // Gecmiste kalan bir tarih: sunucunun gercek saatinden bagimsiz olarak "ayrilmis".
            terminatedOn: new DateOnly(2024, 12, 31));
        await scenario.AddEmployeeAsync(
            scenario.HotelAId,
            scenario.DepartmentAId,
            "Carl",
            "Kuendigung",
            // Uzak gelecek: ihbar suresi icinde, halen kadroda.
            terminatedOn: new DateOnly(2099, 12, 31));

        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        var defaultView = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(Employees);
        var fullView = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            new Uri("api/v1/employees?includeTerminated=true", UriKind.Relative));

        defaultView!.Items.Select(employee => employee.LastName).Should().Equal(
            "Becker", "Kuendigung");
        defaultView.TotalCount.Should().Be(2, "toplam sayac da ayni filtreye tabidir");

        fullView!.Items.Select(employee => employee.LastName).Should().Equal(
            "Becker", "Ehemalig", "Kuendigung");
        fullView.Items.Single(employee => employee.LastName == "Ehemalig").IsActive
            .Should().BeFalse();
        fullView.Items.Single(employee => employee.LastName == "Kuendigung").IsActive
            .Should().BeTrue("ayrilis tarihi gelecekte olan calisan halen aktiftir");
    }

    [RequiresPostgresFact]
    public async Task Search_is_case_insensitive_for_german_umlauts()
    {
        // Bu davranis veritabani collation'ina baglidir: SQLite'in lower() fonksiyonu yalnizca
        // ASCII harfleri kucultur, bu yuzden handler seviyesinde dogrulanamaz.
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Jörg", "Müller");
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");

        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        var byLastName = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            new Uri("api/v1/employees?search=M%C3%9CLLER", UriKind.Relative));
        var byFirstName = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            new Uri("api/v1/employees?search=j%C3%B6rg", UriKind.Relative));

        byLastName!.Items.Select(employee => employee.FullName).Should().Equal("Jörg Müller");
        byFirstName!.Items.Select(employee => employee.FullName).Should().Equal("Jörg Müller");
    }

    [RequiresPostgresFact]
    public async Task Search_matches_the_staff_number_case_insensitively()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        await scenario.AddEmployeeAsync(
            scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker", staffNumber: "p-014");

        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        var page = await client.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            new Uri("api/v1/employees?search=P-014", UriKind.Relative));

        page!.Items.Select(employee => employee.StaffNumber).Should().Equal("p-014");
    }

    [RequiresPostgresFact]
    public async Task Employee_response_matches_the_contract_shape()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        using var client = await scenario.CreateClientAsync(PersonnelPermissions);

        using var response = await client.PostAsJsonAsync(Employees, new
        {
            firstName = "Anna",
            lastName = "Becker",
            email = "anna@hotel.test",
            phone = "+49 30 123456",
            staffNumber = "P-014",
            departmentId = scenario.DepartmentAId,
            employmentType = "PartTime",
            annualLeaveDays = 24.5m,
            hiredOn = "2024-03-01",
            terminatedOn = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<EmployeeResponse>();

        created!.FullName.Should().Be("Anna Becker", "goruntuleme adi sunucuda uretilir");
        created.DepartmentName.Should().Be("Rezeption");
        created.EmploymentType.Should().Be(
            nameof(EmploymentType.PartTime),
            "calisma sekli enum ADI olarak doner, sayi degil");
        created.AnnualLeaveDays.Should().Be(24.5m);
        created.HiredOn.Should().Be(new DateOnly(2024, 3, 1));
        created.TerminatedOn.Should().BeNull();
        created.IsActive.Should().BeTrue();
        created.UserId.Should().BeNull("login iliskisi opsiyoneldir");
    }

    [RequiresPostgresFact]
    public async Task Department_response_reports_the_number_of_live_employees()
    {
        await using var scenario = await SettingsAndPersonnelScenario.StartAsync(fixture);
        var employeeId = await scenario.AddEmployeeAsync(
            scenario.HotelAId, scenario.DepartmentAId, "Anna", "Becker");
        await scenario.AddEmployeeAsync(scenario.HotelAId, scenario.DepartmentAId, "Ben", "Colin");

        using var client = await scenario.CreateClientAsync(PersonnelPermissions);
        using var deleted = await client.DeleteAsync(EmployeeOf(employeeId));

        var departments = await client.GetFromJsonAsync<IReadOnlyList<DepartmentResponse>>(Departments);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        departments!.Single(department => department.Id == scenario.DepartmentAId).EmployeeCount
            .Should().Be(1, "soft-delete edilmis calisan sayilmaz");
    }

    /// <summary>Gecerli bir calisan govdesi.</summary>
    private static object EmployeePayload(
        Guid departmentId,
        string firstName,
        string lastName,
        string? staffNumber = null) => new
        {
            firstName,
            lastName,
            email = (string?)null,
            phone = (string?)null,
            staffNumber,
            departmentId,
            employmentType = "FullTime",
            annualLeaveDays = 28m,
            hiredOn = "2024-03-01",
            terminatedOn = (string?)null
        };
}
