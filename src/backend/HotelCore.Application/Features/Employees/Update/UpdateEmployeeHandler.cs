using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Employees.Common;

namespace HotelCore.Application.Features.Employees.Update;

internal sealed class UpdateEmployeeHandler(IAppDbContext database, EmployeeReader reader)
    : IRequestHandler<UpdateEmployeeRequest, EmployeeResponse>
{
    public async Task<EmployeeResponse> Handle(
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await reader.GetTrackedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        await reader.EnsureDepartmentExistsAsync(request.DepartmentId, cancellationToken)
            .ConfigureAwait(false);
        await reader.EnsureStaffNumberIsFreeAsync(request.StaffNumber, request.Id, cancellationToken)
            .ConfigureAwait(false);

        employee.DepartmentId = request.DepartmentId;
        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.Email = Normalize(request.Email);
        employee.Phone = Normalize(request.Phone);
        employee.StaffNumber = Normalize(request.StaffNumber);
        employee.EmploymentType = request.EmploymentType;
        employee.AnnualLeaveDays = request.AnnualLeaveDays;
        employee.HiredOn = request.HiredOn;
        employee.TerminatedOn = request.TerminatedOn;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
