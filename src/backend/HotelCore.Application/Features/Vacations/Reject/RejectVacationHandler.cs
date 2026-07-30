using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Vacations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Vacations.Reject;

/// <summary>
/// İzin talebini reddeder. Bakiyeye <b>dokunulmaz</b>: gün yalnızca onayda düşüldüğü için
/// reddedilen talebin geri alınacak bir etkisi yoktur.
/// </summary>
internal sealed class RejectVacationHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    VacationReader reader)
    : IRequestHandler<RejectVacationRequest, VacationRequestResponse>
{
    public async Task<VacationRequestResponse> Handle(
        RejectVacationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Yalnizca Pending reddedilebilir; karara baglanmis talebe ikinci karar -> 409.
        var vacation = await reader.GetPendingTrackedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        vacation.Status = VacationStatus.Rejected;

        // Entity alani ApprovedByUserId'dir; kararin sahibini (onay/ret) tutar.
        vacation.ApprovedByUserId = currentUser.UserId;
        vacation.DecidedAt = clock.UtcNow;
        vacation.DecisionNote = string.IsNullOrWhiteSpace(request.DecisionNote)
            ? null
            : request.DecisionNote.Trim();

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(vacation.Id, cancellationToken).ConfigureAwait(false);
    }
}
