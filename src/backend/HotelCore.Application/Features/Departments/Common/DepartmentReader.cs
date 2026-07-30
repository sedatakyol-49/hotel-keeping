using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Departments.Common;

/// <summary>
/// Departman okuma yolu ve paylaşılan kontroller.
/// <para>
/// <c>HotelId</c> filtresi <b>elle yazılmaz</b>: <c>Department</c> tenant-scoped olduğu için
/// <c>AppDbContext</c>'teki global query filter aktif otelle sınırlamayı zaten uygular.
/// </para>
/// </summary>
internal sealed class DepartmentReader(IAppDbContext database)
{
    public async Task<IReadOnlyList<DepartmentResponse>> ListAsync(CancellationToken cancellationToken) =>
        await database.Departments
            .OrderBy(department => department.Name)
            .Select(department => new DepartmentResponse
            {
                Id = department.Id,
                Name = department.Name,
                Description = department.Description,
                EmployeeCount = department.Employees.Count(employee => !employee.IsDeleted),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<DepartmentResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await database.Departments
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new DepartmentResponse
            {
                Id = candidate.Id,
                Name = candidate.Name,
                Description = candidate.Description,
                EmployeeCount = candidate.Employees.Count(employee => !employee.IsDeleted),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return department ?? throw new NotFoundException(Messages.DepartmentNotFound);
    }

    public async Task<Department> GetTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await database.Departments
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return department ?? throw new NotFoundException(Messages.DepartmentNotFound);
    }

    /// <summary>
    /// Ad otel içinde benzersiz olmalıdır. Ön kontrol kullanıcıya anlamlı mesaj verir;
    /// yarış durumunda veritabanı kısıtı yakalanıp 409'a çevrilir (bkz. <c>AppDbContext</c>).
    /// </summary>
    public async Task EnsureNameIsFreeAsync(
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim();

        var exists = await database.Departments
            .AnyAsync(
                department => department.Name == normalized
                              && (excludeId == null || department.Id != excludeId),
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new ConflictException(Messages.DepartmentNameTaken(normalized));
        }
    }
}
