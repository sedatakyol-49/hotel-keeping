using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.Vacations.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Vacations.Create;

/// <summary>
/// İzin talebi oluşturur. Bakiye bu adımda <b>değişmez</b>: gün düşümü yalnızca onayda olur
/// (architecture.md §5), böylece reddedilen/iptal edilen talep bakiyeyi kirletmez.
/// </summary>
internal sealed class CreateVacationHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    EmployeeLookup employees,
    VacationReader reader)
    : IRequestHandler<CreateVacationRequest, VacationRequestResponse>
{
    public async Task<VacationRequestResponse> Handle(
        CreateVacationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Konsolide modda hangi otele yazilacagi belirsizdir -> 400.
        var hotelId = currentUser.RequireHotelId();

        var employee = await employees.GetInHotelAsync(request.EmployeeId, hotelId, cancellationToken)
            .ConfigureAwait(false);

        await reader
            .EnsureNoOverlapAsync(employee.Id, request.From, request.To, excludeId: null, cancellationToken)
            .ConfigureAwait(false);

        var vacation = new VacationRequest
        {
            HotelId = hotelId,
            EmployeeId = employee.Id,
            From = request.From,
            To = request.To,
            RequestedDays = VacationDays.Calculate(request.From, request.To),
            Status = VacationStatus.Pending,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
        };

        database.VacationRequests.Add(vacation);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(vacation.Id, cancellationToken).ConfigureAwait(false);
    }
}
