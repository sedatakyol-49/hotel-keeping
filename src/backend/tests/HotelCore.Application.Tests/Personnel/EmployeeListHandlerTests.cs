using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Models;
using HotelCore.Application.Features.Employees.GetById;
using HotelCore.Application.Features.Employees.List;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Personnel;

/// <summary>
/// <c>GET /employees</c> handler testleri (api-contracts.md → "Personel").
/// <para>
/// Odak: <b>varsayilan gorunumun aktif kadro olmasi</b>, filtreler, sayfalama sayaclari ve
/// sunucuda hesaplanan alanlar (<c>fullName</c>, <c>isActive</c>). "Isten ayrilmis" kavrami
/// takvim gunune bagli oldugu icin saat dondurulmustur (<see cref="TestClock"/>): aksi halde
/// testler gece yarisi kirilgan olurdu.
/// </para>
/// <para>
/// Buradaki arama testleri bilincli olarak <b>ASCII</b> terimler kullanir: SQLite'in
/// <c>lower()</c> fonksiyonu yalnizca ASCII harfleri kucultur. Almanca umlaut / Turkce
/// karakterlerle buyuk-kucuk harf duyarsizligi gercek PostgreSQL'e karsi
/// <c>HotelCore.Api.IntegrationTests</c> icinde dogrulanir.
/// </para>
/// </summary>
public sealed class EmployeeListHandlerTests
{
    [Fact]
    public async Task Terminated_employees_are_hidden_by_default()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(
            host.HotelId,
            host.DepartmentId,
            "Bea",
            "Ehemalig",
            terminatedOn: host.Clock.Today.AddDays(-1));

        var page = await host.Dispatcher.Send(new ListEmployeesRequest());

        page.Items.Select(employee => employee.LastName).Should().Equal("Becker");
        page.TotalCount.Should().Be(1, "toplam sayac da ayni filtreye tabidir");
    }

    [Fact]
    public async Task Employee_whose_last_day_is_in_the_future_is_still_listed_and_active()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(
            host.HotelId,
            host.DepartmentId,
            "Carl",
            "Kuendigung",
            terminatedOn: host.Clock.Today.AddDays(30));

        var page = await host.Dispatcher.Send(new ListEmployeesRequest());

        page.Items.Should().ContainSingle().Which.IsActive.Should().BeTrue(
            "ihbar suresi icindeki calisan halen kadrodadir");
    }

    [Fact]
    public async Task Employee_whose_last_day_is_today_counts_as_inactive()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employee = await host.AddEmployeeAsync(
            host.HotelId,
            host.DepartmentId,
            "Dora",
            "Heute",
            terminatedOn: host.Clock.Today);

        var page = await host.Dispatcher.Send(new ListEmployeesRequest());
        var single = await host.Dispatcher.Send(new GetEmployeeByIdRequest(employee.Id));

        // Sozlesme: isActive = "terminatedOn yok VEYA gelecekte". Bugun gelecek degildir.
        page.Items.Should().BeEmpty();
        single.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Include_terminated_returns_the_former_employees_as_well()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(
            host.HotelId,
            host.DepartmentId,
            "Bea",
            "Ehemalig",
            terminatedOn: host.Clock.Today.AddDays(-1));

        var page = await host.Dispatcher.Send(new ListEmployeesRequest { IncludeTerminated = true });

        page.Items.Select(employee => employee.LastName).Should().Equal("Becker", "Ehemalig");
        page.Items.Single(employee => employee.LastName == "Ehemalig").IsActive.Should().BeFalse();
        page.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Soft_deleted_employees_stay_hidden_even_with_include_terminated()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(
            host.HotelId, host.DepartmentId, "Gone", "Geloescht", isDeleted: true);

        var page = await host.Dispatcher.Send(new ListEmployeesRequest { IncludeTerminated = true });

        page.Items.Select(employee => employee.LastName).Should().Equal("Becker");
        page.TotalCount.Should().Be(
            1,
            "includeTerminated isten ayrilmayi kapsar, soft-delete'i DEGIL");
    }

    [Fact]
    public async Task Listing_returns_only_the_employees_of_the_active_hotel()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(host.OtherHotelId, host.OtherHotelDepartmentId, "Bea", "Bauer");

        var page = await host.Dispatcher.Send(new ListEmployeesRequest());

        page.Items.Select(employee => employee.LastName).Should().Equal("Becker");
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Employees_are_ordered_by_last_name_then_first_name()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Ekleme sirasi bilincli olarak karisik: siralama SQL'den gelmelidir.
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Zoe", "Becker");
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Carl", "Adler");

        var page = await host.Dispatcher.Send(new ListEmployeesRequest());

        page.Items.Select(employee => employee.FullName).Should().Equal(
            "Carl Adler", "Anna Becker", "Zoe Becker");
    }

    [Fact]
    public async Task Search_matches_the_first_name_case_insensitively()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Ben", "Colin");

        var page = await host.Dispatcher.Send(new ListEmployeesRequest { Search = "ANN" });

        page.Items.Select(employee => employee.FullName).Should().Equal("Anna Becker");
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_matches_the_last_name_case_insensitively()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Ben", "Colin");

        var page = await host.Dispatcher.Send(new ListEmployeesRequest { Search = "colin" });

        page.Items.Select(employee => employee.FullName).Should().Equal("Ben Colin");
    }

    [Fact]
    public async Task Search_matches_the_staff_number()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker", "p-014");
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Ben", "Colin", "P-015");

        var page = await host.Dispatcher.Send(new ListEmployeesRequest { Search = "P-01" });

        page.Items.Select(employee => employee.StaffNumber).Should().Equal("p-014", "P-015");

        var exact = await host.Dispatcher.Send(new ListEmployeesRequest { Search = "P-014" });
        exact.Items.Select(employee => employee.FullName).Should().Equal("Anna Becker");
    }

    [Fact]
    public async Task Search_does_not_reach_into_another_hotel()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(
            host.OtherHotelId, host.OtherHotelDepartmentId, "Anna", "Becker", "P-014");

        var page = await host.Dispatcher.Send(new ListEmployeesRequest { Search = "Becker" });

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Department_filter_narrows_the_result_and_the_total_count()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var kitchen = await host.AddDepartmentAsync(host.HotelId, "Kueche");
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(host.HotelId, kitchen.Id, "Ben", "Colin");

        var page = await host.Dispatcher.Send(
            new ListEmployeesRequest { DepartmentId = kitchen.Id });

        page.Items.Select(employee => employee.FullName).Should().Equal("Ben Colin");
        page.Items.Should().ContainSingle().Which.DepartmentName.Should().Be("Kueche");
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Employment_type_filter_narrows_the_result()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(
            host.HotelId,
            host.DepartmentId,
            "Ben",
            "Colin",
            employmentType: EmploymentType.MiniJob);

        var page = await host.Dispatcher.Send(
            new ListEmployeesRequest { EmploymentType = EmploymentType.MiniJob });

        page.Items.Select(employee => employee.FullName).Should().Equal("Ben Colin");
        page.Items.Should().ContainSingle().Which.EmploymentType.Should().Be(
            nameof(EmploymentType.MiniJob));
    }

    [Fact]
    public async Task Filters_combine_with_the_default_active_only_view()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(
            host.HotelId,
            host.DepartmentId,
            "Bea",
            "Ehemalig",
            employmentType: EmploymentType.FullTime,
            terminatedOn: host.Clock.Today.AddDays(-5));

        var page = await host.Dispatcher.Send(
            new ListEmployeesRequest { EmploymentType = EmploymentType.FullTime });

        page.Items.Select(employee => employee.LastName).Should().Equal("Becker");
    }

    [Fact]
    public async Task Paging_reports_the_requested_page_and_the_unpaged_total()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        foreach (var lastName in new[] { "Adler", "Becker", "Colin", "Decker", "Ebert" })
        {
            await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Test", lastName);
        }

        var page = await host.Dispatcher.Send(new ListEmployeesRequest { Page = 2, PageSize = 2 });

        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.TotalCount.Should().Be(5, "toplam sayac sayfalamadan ONCE hesaplanir");
        page.Items.Select(employee => employee.LastName).Should().Equal("Colin", "Decker");
    }

    [Fact]
    public async Task Paging_is_stable_for_employees_sharing_the_same_name()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        for (var index = 0; index < 4; index++)
        {
            await host.AddEmployeeAsync(
                host.HotelId, host.DepartmentId, "Anna", "Becker", $"P-{index:D3}");
        }

        var firstPage = await host.Dispatcher.Send(new ListEmployeesRequest { PageSize = 2 });
        var secondPage = await host.Dispatcher.Send(
            new ListEmployeesRequest { Page = 2, PageSize = 2 });

        // Ad esitliginde Id ile kirilan siralama sayesinde sayfalar ortusmez.
        firstPage.Items.Select(employee => employee.Id)
            .Should().NotIntersectWith(secondPage.Items.Select(employee => employee.Id));
        firstPage.Items.Should().HaveCount(2);
        secondPage.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Page_size_above_the_limit_is_rejected_instead_of_being_silently_changed()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Istemci sessizce farkli bir sayfa boyutu almamalidir; ust sinir asilirsa 400 doner.
        var act = async () => await host.Dispatcher.Send(
            new ListEmployeesRequest { PageSize = PageQuery.MaxPageSize + 1 });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Page_size_at_the_upper_limit_is_accepted()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var page = await host.Dispatcher.Send(
            new ListEmployeesRequest { PageSize = PageQuery.MaxPageSize });

        page.PageSize.Should().Be(PageQuery.MaxPageSize);
    }

    [Fact]
    public async Task Single_employee_response_exposes_the_contract_fields()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employee = await host.AddEmployeeAsync(
            host.HotelId,
            host.DepartmentId,
            "Anna",
            "Becker",
            staffNumber: "P-014",
            employmentType: EmploymentType.PartTime,
            hiredOn: new DateOnly(2024, 3, 1),
            email: "anna@hotel.test",
            phone: "+49 30 123",
            annualLeaveDays: 24.5m);

        var response = await host.Dispatcher.Send(new GetEmployeeByIdRequest(employee.Id));

        response.Id.Should().Be(employee.Id);
        response.FullName.Should().Be("Anna Becker");
        response.Email.Should().Be("anna@hotel.test");
        response.Phone.Should().Be("+49 30 123");
        response.StaffNumber.Should().Be("P-014");
        response.DepartmentId.Should().Be(host.DepartmentId);
        response.DepartmentName.Should().Be("Rezeption");
        response.EmploymentType.Should().Be(nameof(EmploymentType.PartTime));
        response.AnnualLeaveDays.Should().Be(24.5m);
        response.HiredOn.Should().Be(new DateOnly(2024, 3, 1));
        response.TerminatedOn.Should().BeNull();
        response.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Empty_hotel_returns_an_empty_page_instead_of_failing()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var page = await host.Dispatcher.Send(new ListEmployeesRequest());

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.Page.Should().Be(1);
    }
}
