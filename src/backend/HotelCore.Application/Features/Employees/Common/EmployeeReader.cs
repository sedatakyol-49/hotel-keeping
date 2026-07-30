using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Models;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Employees.Common;

/// <summary>
/// Çalışan yanıtlarının tek üretim noktası.
/// <para>
/// Tenant izolasyonu ve soft-delete <c>AppDbContext</c> global query filter'ından gelir;
/// burada <c>HotelId</c>/<c>IsDeleted</c> koşulu YAZILMAZ.
/// </para>
/// </summary>
internal sealed class EmployeeReader(IAppDbContext database, IDateTimeProvider clock)
{
    public async Task<PagedResult<EmployeeResponse>> ListAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filtered = ApplyFilters(database.Employees, query, Today());

        var totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await filtered
            // Ad sirasi: soyad, ad, sonra Id (sayfalama esitlikte kararli kalsin).
            .OrderBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .ThenBy(employee => employee.Id)
            .Skip(query.Paging.Skip)
            .Take(query.Paging.PageSize)
            .Project(Today())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<EmployeeResponse>(
            items,
            query.Paging.Page,
            query.Paging.PageSize,
            totalCount);
    }

    /// <summary>Tek çalışan; bulunamazsa (veya başka otele aitse) 404.</summary>
    public async Task<EmployeeResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await database.Employees
            .Where(candidate => candidate.Id == id)
            .Project(Today())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return employee ?? throw new NotFoundException(nameof(Employee), id);
    }

    public async Task<Employee> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await database.Employees
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return employee ?? throw new NotFoundException(nameof(Employee), id);
    }

    /// <summary>
    /// Departman aktif otelde olmalıdır. Global query filter başka otelin departmanını
    /// gizlediği için burada "bulunamadı" (404) yanıtı doğru olan davranıştır — başka bir
    /// otelin departmanına çalışan bağlanamaz.
    /// </summary>
    public async Task EnsureDepartmentExistsAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var exists = await database.Departments
            .AnyAsync(department => department.Id == departmentId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new NotFoundException(Messages.DepartmentNotFound);
        }
    }

    /// <summary>
    /// Personel numarası (verilmişse) otel içinde benzersiz olmalıdır. Ön kontrol anlamlı
    /// mesaj verir; yarış durumunda veritabanı kısıtı 409'a çevrilir (bkz. <c>AppDbContext</c>).
    /// </summary>
    public async Task EnsureStaffNumberIsFreeAsync(
        string? staffNumber,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(staffNumber))
        {
            return;
        }

        var normalized = staffNumber.Trim();

        var exists = await database.Employees
            .AnyAsync(
                employee => employee.StaffNumber == normalized
                            && (excludeId == null || employee.Id != excludeId),
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new ConflictException(Messages.StaffNumberTaken(normalized));
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    private static IQueryable<Employee> ApplyFilters(
        IQueryable<Employee> query,
        EmployeeListQuery filter,
        DateOnly today)
    {
        if (filter.DepartmentId is Guid departmentId)
        {
            query = query.Where(employee => employee.DepartmentId == departmentId);
        }

        if (filter.EmploymentType is EmploymentType employmentType)
        {
            query = query.Where(employee => employee.EmploymentType == employmentType);
        }

        if (!filter.IncludeTerminated)
        {
            query = query.Where(employee =>
                employee.TerminatedOn == null || employee.TerminatedOn > today);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Büyük/küçük harf duyarsız "contains": terim C# tarafında küçültülür, kolonlar
            // SQL'de lower(...) ile küçültülür. Sonuç veritabanı collation'ına bağlıdır.
            var term = filter.Search.Trim().ToLowerInvariant();

            // CA1304/CA1311/CA1862 bastırılır: kültür parametreli aşırı yüklemeleri EF Core
            // SQL'e çeviremez (oda modülündeki arama ile aynı gerekçe).
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(employee =>
                employee.FirstName.ToLower().Contains(term)
                || employee.LastName.ToLower().Contains(term)
                || (employee.StaffNumber != null && employee.StaffNumber.ToLower().Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return query;
    }
}

/// <summary>Çalışan izdüşümü — departman adı JOIN ile alınır (Include yerine izdüşüm).</summary>
internal static class EmployeeQueryExtensions
{
    public static IQueryable<EmployeeResponse> Project(this IQueryable<Employee> query, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query.Select(employee => new EmployeeResponse
        {
            Id = employee.Id,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            FullName = employee.FirstName + " " + employee.LastName,
            Email = employee.Email,
            Phone = employee.Phone,
            StaffNumber = employee.StaffNumber,
            DepartmentId = employee.DepartmentId,
            DepartmentName = employee.Department.Name ?? string.Empty,
            EmploymentType = employee.EmploymentType.ToString(),
            AnnualLeaveDays = employee.AnnualLeaveDays,
            HiredOn = employee.HiredOn,
            TerminatedOn = employee.TerminatedOn,
            IsActive = employee.TerminatedOn == null || employee.TerminatedOn > today,
            UserId = employee.UserId,
        });
    }
}
