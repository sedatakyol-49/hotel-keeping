using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Departments.Common;
using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.Departments.Create;

internal sealed class CreateDepartmentHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    DepartmentReader reader)
    : IRequestHandler<CreateDepartmentRequest, DepartmentResponse>
{
    public async Task<DepartmentResponse> Handle(
        CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Konsolide modda hangi otele yazilacagi belirsizdir -> 400.
        var hotelId = currentUser.RequireHotelId();

        await reader.EnsureNameIsFreeAsync(request.Name, excludeId: null, cancellationToken)
            .ConfigureAwait(false);

        // Id, EntityBase tarafindan uretilir (proje konvansiyonu); elle atanmaz.
        var department = new Department
        {
            HotelId = hotelId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
        };

        database.Departments.Add(department);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(department.Id, cancellationToken).ConfigureAwait(false);
    }
}
