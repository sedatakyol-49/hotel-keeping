using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Departments.Common;

namespace HotelCore.Application.Features.Departments.Update;

internal sealed class UpdateDepartmentHandler(IAppDbContext database, DepartmentReader reader)
    : IRequestHandler<UpdateDepartmentRequest, DepartmentResponse>
{
    public async Task<DepartmentResponse> Handle(
        UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var department = await reader.GetTrackedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        await reader.EnsureNameIsFreeAsync(request.Name, request.Id, cancellationToken)
            .ConfigureAwait(false);

        department.Name = request.Name.Trim();
        department.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }
}
