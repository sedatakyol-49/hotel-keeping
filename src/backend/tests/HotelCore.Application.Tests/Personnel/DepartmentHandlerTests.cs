using AwesomeAssertions;
using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Features.Departments.Create;
using HotelCore.Application.Features.Departments.Delete;
using HotelCore.Application.Features.Departments.List;
using HotelCore.Application.Features.Departments.Update;
using HotelCore.Application.Tests.Support;

namespace HotelCore.Application.Tests.Personnel;

/// <summary>
/// Departman handler testleri (api-contracts.md → "Personel").
/// <para>
/// Sozlesme: ad otel icinde benzersizdir (<b>409</b>); guncellemede kaydin <b>kendi</b> adi
/// cakisma sayilmaz; silme <b>gercek silmedir</b> (departman bir siniflandirmadir, gecmis kayit
/// tasimaz) ve bagli calisan varken engellenir (<b>409</b>).
/// </para>
/// </summary>
public sealed class DepartmentHandlerTests
{
    [Fact]
    public async Task Department_is_created_in_the_active_hotel()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(new CreateDepartmentRequest
        {
            Name = "Housekeeping",
            Description = "Etage"
        });

        created.Name.Should().Be("Housekeeping");
        created.Description.Should().Be("Etage");
        created.EmployeeCount.Should().Be(0);
        (await host.FindDepartmentAsync(created.Id))!.HotelId.Should().Be(host.HotelId);
    }

    [Fact]
    public async Task Name_and_description_are_trimmed_and_a_blank_description_becomes_null()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var created = await host.Dispatcher.Send(new CreateDepartmentRequest
        {
            Name = "  Kueche  ",
            Description = "   "
        });

        created.Name.Should().Be("Kueche");
        created.Description.Should().BeNull();
    }

    [Fact]
    public async Task Duplicate_department_name_in_the_same_hotel_is_rejected_with_a_conflict()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // "Rezeption" sahnede zaten var.
        var act = async () =>
            await host.Dispatcher.Send(new CreateDepartmentRequest { Name = "Rezeption" });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Duplicate_check_ignores_surrounding_whitespace()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Ad kirpilarak saklandigi icin "  Rezeption  " ayni addir.
        var act = async () =>
            await host.Dispatcher.Send(new CreateDepartmentRequest { Name = "  Rezeption  " });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task The_same_department_name_may_be_used_in_another_hotel()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Aktif otel B'ye gecilir: benzersizlik otel icindedir, sistem genelinde degil.
        host.CurrentUser.HotelId = host.OtherHotelId;

        var created = await host.Dispatcher.Send(new CreateDepartmentRequest { Name = "Rezeption" });

        created.Name.Should().Be("Rezeption");
        (await host.FindDepartmentAsync(created.Id))!.HotelId.Should().Be(host.OtherHotelId);
    }

    [Fact]
    public async Task Creating_a_department_without_an_active_hotel_is_rejected_with_a_validation_error()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // Head Office konsolide modu: kaydin hangi otele yazilacagi belirsizdir.
        host.CurrentUser.CanAccessAllHotels = true;
        host.CurrentUser.HotelId = null;

        var act = async () =>
            await host.Dispatcher.Send(new CreateDepartmentRequest { Name = "Technik" });

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("X-Hotel-Id");
    }

    [Fact]
    public async Task Renaming_a_department_to_its_own_name_is_not_a_conflict()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        // excludeId: kaydin kendisi cakisma sayilmaz, aksi halde aciklama guncellenemezdi.
        var updated = await host.Dispatcher.Send(new UpdateDepartmentRequest
        {
            Id = host.DepartmentId,
            Name = "Rezeption",
            Description = "Neuer Text"
        });

        updated.Name.Should().Be("Rezeption");
        updated.Description.Should().Be("Neuer Text");
    }

    [Fact]
    public async Task Renaming_a_department_to_the_name_of_another_department_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var housekeeping = await host.AddDepartmentAsync(host.HotelId, "Housekeeping");

        var act = async () => await host.Dispatcher.Send(new UpdateDepartmentRequest
        {
            Id = housekeeping.Id,
            Name = "Rezeption"
        });

        await act.Should().ThrowAsync<ConflictException>();
        (await host.FindDepartmentAsync(housekeeping.Id))!.Name.Should().Be("Housekeeping");
    }

    [Fact]
    public async Task Updating_a_department_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new UpdateDepartmentRequest
        {
            Id = host.OtherHotelDepartmentId,
            Name = "Uebernommen"
        });

        await act.Should().ThrowAsync<NotFoundException>();
        (await host.FindDepartmentAsync(host.OtherHotelDepartmentId))!.Name.Should().Be("Rezeption B");
    }

    [Fact]
    public async Task Deleting_an_empty_department_removes_the_row_physically()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        var technik = await host.AddDepartmentAsync(host.HotelId, "Technik");

        await host.Dispatcher.Send(new DeleteDepartmentRequest(technik.Id));

        (await host.FindDepartmentAsync(technik.Id)).Should().BeNull(
            "departman soft-delete EDILEMEZ; silme gercek silmedir");
    }

    [Fact]
    public async Task Deleting_a_department_with_employees_is_rejected_with_a_conflict()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");

        var act = async () => await host.Dispatcher.Send(new DeleteDepartmentRequest(host.DepartmentId));

        await act.Should().ThrowAsync<ConflictException>();
        (await host.FindDepartmentAsync(host.DepartmentId)).Should().NotBeNull(
            "reddedilen silme islemi satiri kaldirmamalidir");
    }

    [Fact]
    public async Task Deleting_a_department_of_another_hotel_is_reported_as_not_found()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () =>
            await host.Dispatcher.Send(new DeleteDepartmentRequest(host.OtherHotelDepartmentId));

        await act.Should().ThrowAsync<NotFoundException>();
        (await host.FindDepartmentAsync(host.OtherHotelDepartmentId)).Should().NotBeNull();
    }

    [Fact]
    public async Task Employee_count_ignores_soft_deleted_employees()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Anna", "Becker");
        await host.AddEmployeeAsync(host.HotelId, host.DepartmentId, "Ben", "Colin", isDeleted: true);

        var departments = await host.Dispatcher.Send(new ListDepartmentsRequest());

        departments.Should().ContainSingle(department => department.Id == host.DepartmentId)
            .Which.EmployeeCount.Should().Be(1);
    }

    [Fact]
    public async Task Listing_returns_only_the_departments_of_the_active_hotel()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();
        await host.AddDepartmentAsync(host.HotelId, "Housekeeping");

        var departments = await host.Dispatcher.Send(new ListDepartmentsRequest());

        departments.Select(department => department.Name).Should().BeEquivalentTo(
            ["Rezeption", "Housekeeping"]);
        departments.Should().NotContain(department => department.Id == host.OtherHotelDepartmentId);
    }

    [Fact]
    public async Task Empty_name_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () =>
            await host.Dispatcher.Send(new CreateDepartmentRequest { Name = "   " });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Description_longer_than_five_hundred_characters_is_rejected()
    {
        await using var host = await SettingsAndPersonnelTestHost.CreateAsync();

        var act = async () => await host.Dispatcher.Send(new CreateDepartmentRequest
        {
            Name = "Technik",
            Description = new string('x', 501)
        });

        await act.Should().ThrowAsync<ValidationException>();
    }
}
