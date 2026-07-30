using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Departments.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Departments.Delete;

internal sealed class DeleteDepartmentHandler(IAppDbContext database, DepartmentReader reader)
    : IRequestHandler<DeleteDepartmentRequest, Unit>
{
    public async Task<Unit> Handle(
        DeleteDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var department = await reader.GetTrackedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        // Bagli calisan varken silmek calisanin departmanini bosta birakirdi (FK Restrict);
        // kullaniciya 500 yerine anlamli bir 409 dondurulur.
        var hasEmployees = await database.Employees
            .AnyAsync(employee => employee.DepartmentId == department.Id, cancellationToken)
            .ConfigureAwait(false);

        if (hasEmployees)
        {
            throw new ConflictException(
                "Bu departmana bagli calisanlar var; once onlari baska departmana tasiyin.");
        }

        database.Departments.Remove(department);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
