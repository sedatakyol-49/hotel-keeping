using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.Shifts.Common;

namespace HotelCore.Application.Features.Shifts.Update;

internal sealed class UpdateShiftHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    EmployeeLookup employees,
    ShiftReader reader)
    : IRequestHandler<UpdateShiftRequest, ShiftResponse>
{
    public async Task<ShiftResponse> Handle(
        UpdateShiftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotelId = currentUser.RequireHotelId();

        var shift = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        // Vardiya baska otelin calisanina tasinamaz (404) — tenant butunlugu korunur.
        var employee = await employees.GetInHotelAsync(request.EmployeeId, hotelId, cancellationToken)
            .ConfigureAwait(false);

        await reader.EnsureDateIsFreeAsync(employee.Id, request.Date, shift.Id, cancellationToken)
            .ConfigureAwait(false);

        shift.EmployeeId = employee.Id;
        shift.Date = request.Date;
        shift.ShiftType = request.ShiftType;
        shift.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(shift.Id, cancellationToken).ConfigureAwait(false);
    }
}
