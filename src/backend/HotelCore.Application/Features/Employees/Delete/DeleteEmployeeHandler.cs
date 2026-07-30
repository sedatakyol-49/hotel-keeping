using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Employees.Common;

namespace HotelCore.Application.Features.Employees.Delete;

internal sealed class DeleteEmployeeHandler(IAppDbContext database, EmployeeReader reader)
    : IRequestHandler<DeleteEmployeeRequest, Unit>
{
    public async Task<Unit> Handle(
        DeleteEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await reader.GetTrackedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        // AppDbContext Deleted -> Modified'a cevirip IsDeleted/DeletedAt damgalar:
        // izin ve zaman kayitlari (FK Restrict) korunur.
        database.Employees.Remove(employee);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
