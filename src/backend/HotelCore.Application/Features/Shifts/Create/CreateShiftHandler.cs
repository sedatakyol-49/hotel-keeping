using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.Shifts.Common;
using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.Shifts.Create;

internal sealed class CreateShiftHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    EmployeeLookup employees,
    ShiftReader reader)
    : IRequestHandler<CreateShiftRequest, ShiftResponse>
{
    public async Task<ShiftResponse> Handle(
        CreateShiftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Konsolide modda hangi otele yazilacagi belirsizdir -> 400.
        var hotelId = currentUser.RequireHotelId();

        var employee = await employees.GetInHotelAsync(request.EmployeeId, hotelId, cancellationToken)
            .ConfigureAwait(false);

        await reader.EnsureDateIsFreeAsync(employee.Id, request.Date, excludeId: null, cancellationToken)
            .ConfigureAwait(false);

        var shift = new Shift
        {
            HotelId = hotelId,
            EmployeeId = employee.Id,
            Date = request.Date,
            ShiftType = request.ShiftType,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };

        database.Shifts.Add(shift);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(shift.Id, cancellationToken).ConfigureAwait(false);
    }
}
