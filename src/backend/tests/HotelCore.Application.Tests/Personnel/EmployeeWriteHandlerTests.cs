using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Employees.Create;
using HotelCore.Application.Features.Employees.Delete;
using HotelCore.Application.Features.Employees.GetById;
using HotelCore.Application.Features.Employees.Update;
using HotelCore.Application.Tests.Support;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Tests.Personnel;

/// <summary>
/// Calisan yazma yolu handler testleri (api-contracts.md → "Personel").
/// <para>
/// Sozlesme: <c>staffNumber</c> verilirse otel icinde benzersizdir (<b>409</b>); departman
/// <b>ayni otelde</b> olmalidir (aksi halde <b>404</b>); silme <b>soft-delete</b>'tir — izin ve
/// zaman kayitlari korunur, kayit yalnizca listelerden duser.
/// </para>
/// </summary>
public sealed class EmployeeWriteHandlerTests
{
    [Fact]
    public async Task Employee_is_created_in_the_active_hotel_with_server_computed_fields()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request(host.DepartmentId) with
        {
            FirstName = "Anna",
            LastName = "Becker",
            StaffNumber = "P-014",
            AnnualLeaveDays = 28m,
            HiredOn = new DateOnly(2024, 3, 1)
        });

        created.FullName.Should().Be("Anna Becker", "goruntuleme adi sunucuda uretilir");
        created.DepartmentName.Should().Be("Rezeption", "departman adi izdusumde JOIN ile gelir");
        created.EmploymentType.Should().Be(
            nameof(EmploymentType.FullTime),
            "calisma sekli enum ADI olarak doner, sayi degil");
        created.IsActive.Should().BeTrue();
        created.UserId.Should().BeNull("her calisanin sisteme girisi olmayabilir");
        (await host.FindEmployeeIncludingDeletedAsync(created.Id))!.HotelId.Should().Be(host.HotelId);
    }

    [Fact]
    public async Task Names_are_trimmed_and_blank_optional_fields_are_stored_as_null()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(Request(host.DepartmentId) with
        {
            FirstName = "  Anna  ",
            LastName = "  Becker  ",
            Email = "   ",
            Phone = string.Empty,
            StaffNumber = "\t"
        });

        created.FirstName.Should().Be("Anna");
        created.LastName.Should().Be("Becker");
        created.FullName.Should().Be("Anna Becker");
        created.Email.Should().BeNull();
        created.Phone.Should().BeNull();
        created.StaffNumber.Should().BeNull();
    }

    [Fact]
    public async Task Duplicate_staff_number_in_the_same_hotel_is_rejected_with_a_conflict()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker", "P-014");

        var act = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { StaffNumber = "P-014" });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Duplicate_staff_number_check_ignores_surrounding_whitespace()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker", "P-014");

        var act = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { StaffNumber = "  P-014  " });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task The_same_staff_number_may_be_used_in_another_hotel()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker", "P-014");

        // Benzersizlik otel icindedir; B otelinde ayni numara serbesttir.
        host.CurrentUser.HotelId = host.OtherHotelId;

        var created = await host.Dispatcher.Send(
            Request(host.OtherHotelDepartmentId) with { StaffNumber = "P-014" });

        created.StaffNumber.Should().Be("P-014");
    }

    [Fact]
    public async Task Several_employees_may_omit_the_staff_number()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var first = await host.Dispatcher.Send(Request(host.DepartmentId) with
        {
            FirstName = "Anna",
            LastName = "Becker",
            StaffNumber = null
        });
        var second = await host.Dispatcher.Send(Request(host.DepartmentId) with
        {
            FirstName = "Ben",
            LastName = "Colin",
            StaffNumber = null
        });

        first.StaffNumber.Should().BeNull();
        second.StaffNumber.Should().BeNull(
            "personel numarasi opsiyoneldir; bos deger benzersizlik kisitina girmez");
    }

    [Fact]
    public async Task Attaching_an_employee_to_a_department_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(Request(host.OtherHotelDepartmentId));

        // 404: global query filter baska otelin departmanini gizler, "yok" dogru yanittir.
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Unknown_department_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(Request(Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Creating_an_employee_without_an_active_hotel_is_rejected_with_a_validation_error()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Head Office konsolide modu: kaydin hangi otele yazilacagi belirsizdir.
        host.CurrentUser.CanAccessAllHotels = true;
        host.CurrentUser.HotelId = null;

        var act = async () => await host.Dispatcher.Send(Request(host.DepartmentId));

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("X-Hotel-Id");
    }

    [Fact]
    public async Task Keeping_the_own_staff_number_while_updating_is_not_a_conflict()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employee = await host.AddEmployeeAsync(
            host.HotelId, host.DepartmentId, "Anna", "Becker", "P-014");

        // excludeId: kaydin kendisi cakisma sayilmaz, aksi halde telefon bile guncellenemezdi.
        var updated = await host.Dispatcher.Send(new UpdateEmployeeRequest
        {
            Id = employee.Id,
            FirstName = "Anna",
            LastName = "Beckmann",
            StaffNumber = "P-014",
            DepartmentId = host.DepartmentId,
            EmploymentType = EmploymentType.PartTime,
            AnnualLeaveDays = 20m,
            HiredOn = employee.HiredOn
        });

        updated.LastName.Should().Be("Beckmann");
        updated.StaffNumber.Should().Be("P-014");
        updated.EmploymentType.Should().Be(nameof(EmploymentType.PartTime));
        updated.AnnualLeaveDays.Should().Be(20m);
    }

    [Fact]
    public async Task Taking_over_the_staff_number_of_another_employee_is_rejected_with_a_conflict()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker", "P-014");
        var second = await host.AddEmployeeAsync(
            host.HotelId, host.DepartmentId, "Ben", "Colin", "P-015");

        var act = async () => await host.Dispatcher.Send(new UpdateEmployeeRequest
        {
            Id = second.Id,
            FirstName = "Ben",
            LastName = "Colin",
            StaffNumber = "P-014",
            DepartmentId = host.DepartmentId,
            HiredOn = second.HiredOn
        });

        await act.Should().ThrowAsync<ConflictException>();
        (await host.FindEmployeeIncludingDeletedAsync(second.Id))!.StaffNumber.Should().Be("P-015");
    }

    [Fact]
    public async Task Updating_an_employee_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employeeInB = await host.AddEmployeeAsync(
            host.OtherHotelId, host.OtherHotelDepartmentId, "Bea", "Bauer");

        var act = async () => await host.Dispatcher.Send(new UpdateEmployeeRequest
        {
            Id = employeeInB.Id,
            FirstName = "Uebernommen",
            LastName = "Bauer",
            DepartmentId = host.DepartmentId,
            HiredOn = employeeInB.HiredOn
        });

        await act.Should().ThrowAsync<NotFoundException>();
        (await host.FindEmployeeIncludingDeletedAsync(employeeInB.Id))!.FirstName.Should().Be("Bea");
    }

    [Fact]
    public async Task Moving_an_employee_to_a_department_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employee = await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");

        var act = async () => await host.Dispatcher.Send(new UpdateEmployeeRequest
        {
            Id = employee.Id,
            FirstName = "Anna",
            LastName = "Becker",
            DepartmentId = host.OtherHotelDepartmentId,
            HiredOn = employee.HiredOn
        });

        await act.Should().ThrowAsync<NotFoundException>();
        (await host.FindEmployeeIncludingDeletedAsync(employee.Id))!.DepartmentId
            .Should().Be(host.DepartmentId);
    }

    [Fact]
    public async Task Deleting_an_employee_is_a_soft_delete_that_keeps_the_row()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employee = await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");

        await host.Dispatcher.Send(new DeleteEmployeeRequest(employee.Id));

        var stored = await host.FindEmployeeIncludingDeletedAsync(employee.Id);
        stored.Should().NotBeNull("izin/zaman kayitlari korunur; satir fiziksel olarak SILINMEZ");
        stored!.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().BeCloseTo(host.Clock.UtcNow, TimeSpan.FromSeconds(1));
        stored.HotelId.Should().Be(host.HotelId);
        stored.DepartmentId.Should().Be(host.DepartmentId);
    }

    [Fact]
    public async Task Soft_deleted_employee_is_no_longer_readable()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employee = await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");

        await host.Dispatcher.Send(new DeleteEmployeeRequest(employee.Id));
        var act = async () => await host.Dispatcher.Send(new GetEmployeeByIdRequest(employee.Id));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Deleting_an_employee_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employeeInB = await host.AddEmployeeAsync(
            host.OtherHotelId, host.OtherHotelDepartmentId, "Bea", "Bauer");

        var act = async () => await host.Dispatcher.Send(new DeleteEmployeeRequest(employeeInB.Id));

        await act.Should().ThrowAsync<NotFoundException>();
        (await host.FindEmployeeIncludingDeletedAsync(employeeInB.Id))!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Employee_of_another_hotel_is_reported_as_not_found_instead_of_forbidden()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var employeeInB = await host.AddEmployeeAsync(
            host.OtherHotelId, host.OtherHotelDepartmentId, "Bea", "Bauer");

        var act = async () => await host.Dispatcher.Send(new GetEmployeeByIdRequest(employeeInB.Id));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Termination_date_before_the_hire_date_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(Request(host.DepartmentId) with
        {
            HiredOn = new DateOnly(2026, 1, 15),
            TerminatedOn = new DateOnly(2026, 1, 14)
        });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey(nameof(CreateEmployeeRequest.TerminatedOn));
    }

    [Fact]
    public async Task Termination_on_the_hire_date_is_accepted()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Sinir durumu: ayni gun ise girip ayrilan calisan (deneme suresi ihlali) mesrudur.
        var created = await host.Dispatcher.Send(Request(host.DepartmentId) with
        {
            HiredOn = new DateOnly(2026, 1, 15),
            TerminatedOn = new DateOnly(2026, 1, 15)
        });

        created.TerminatedOn.Should().Be(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public async Task Annual_leave_days_outside_the_allowed_range_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var tooMany = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { AnnualLeaveDays = 61m });
        var negative = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { AnnualLeaveDays = -1m });

        await tooMany.Should().ThrowAsync<ValidationException>();
        await negative.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Half_days_of_annual_leave_are_accepted()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(
            Request(host.DepartmentId) with { AnnualLeaveDays = 27.5m });

        created.AnnualLeaveDays.Should().Be(27.5m, "yarim gunler icin ondalik desteklenir");
    }

    [Fact]
    public async Task Employment_type_outside_the_enum_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { EmploymentType = (EmploymentType)99 });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Missing_first_or_last_name_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var noFirstName = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { FirstName = "   " });
        var noLastName = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { LastName = string.Empty });

        await noFirstName.Should().ThrowAsync<ValidationException>();
        await noLastName.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Invalid_email_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(
            Request(host.DepartmentId) with { Email = "anna(at)hotel.test" });

        await act.Should().ThrowAsync<ValidationException>();
    }

    /// <summary>Gecerli bir temel istek; testler yalnizca ilgilendikleri alani degistirir.</summary>
    private static CreateEmployeeRequest Request(Guid departmentId) => new()
    {
        FirstName = "Anna",
        LastName = "Becker",
        DepartmentId = departmentId,
        EmploymentType = EmploymentType.FullTime,
        AnnualLeaveDays = 28m,
        HiredOn = new DateOnly(2024, 3, 1)
    };
}
