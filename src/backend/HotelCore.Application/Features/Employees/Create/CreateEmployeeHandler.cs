using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Employees.Common;
using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.Employees.Create;

internal sealed class CreateEmployeeHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    EmployeeReader reader)
    : IRequestHandler<CreateEmployeeRequest, EmployeeResponse>
{
    public async Task<EmployeeResponse> Handle(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Konsolide modda hangi otele yazilacagi belirsizdir -> 400.
        var hotelId = currentUser.RequireHotelId();

        await reader.EnsureDepartmentExistsAsync(request.DepartmentId, cancellationToken)
            .ConfigureAwait(false);
        await reader.EnsureStaffNumberIsFreeAsync(request.StaffNumber, null, cancellationToken)
            .ConfigureAwait(false);

        var employee = new Employee
        {
            HotelId = hotelId,
            DepartmentId = request.DepartmentId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = Normalize(request.Email),
            Phone = Normalize(request.Phone),
            StaffNumber = Normalize(request.StaffNumber),
            EmploymentType = request.EmploymentType,
            AnnualLeaveDays = request.AnnualLeaveDays,
            HiredOn = request.HiredOn,
            TerminatedOn = request.TerminatedOn,
        };

        database.Employees.Add(employee);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(employee.Id, cancellationToken).ConfigureAwait(false);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
