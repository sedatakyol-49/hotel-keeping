using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.Vacations.Common;
using HotelCore.Domain.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Vacations.Cancel;

/// <summary>
/// İzin talebini iptal eder.
/// <para>
/// <b>Bakiye:</b> talep <c>Approved</c> ise gün sayısı <c>UsedDays</c>'ten <b>geri düşülür</b>
/// (architecture.md §5); <c>Pending</c> talebin bakiyeye etkisi hiç olmadığı için düşülecek bir
/// şey yoktur. Durum değişikliği ve bakiye düzeltmesi <b>tek</b> <c>SaveChangesAsync</c> ile
/// (tek transaction) yazılır.
/// </para>
/// <para>
/// <b>Yetki:</b> <c>Vacations.Approve</c> olan kullanıcı her talebi iptal edebilir; yalnızca
/// <c>Vacations.Request</c> olan kullanıcı <b>kendi</b> talebini (çalışan kaydı kendi
/// kullanıcısına bağlıysa) iptal edebilir. İki alternatifli izin tek policy ile ifade
/// edilemediği için kontrol burada yapılır ve yetkisiz istek 403 döner.
/// </para>
/// </summary>
internal sealed class CancelVacationHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    EmployeeLookup employees,
    VacationReader reader)
    : IRequestHandler<CancelVacationRequest, VacationRequestResponse>
{
    public async Task<VacationRequestResponse> Handle(
        CancelVacationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vacation = await reader.GetTrackedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        var employee = await employees.GetTrackedAsync(vacation.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        EnsureCanCancel(employee.UserId);

        if (vacation.Status is not (VacationStatus.Pending or VacationStatus.Approved))
        {
            throw new ConflictException(Messages.VacationNotCancellable(vacation.Status));
        }

        if (vacation.Status is VacationStatus.Approved)
        {
            var balance = await reader
                .FindBalanceAsync(vacation.EmployeeId, vacation.From.Year, cancellationToken)
                .ConfigureAwait(false);

            if (balance is not null)
            {
                // Negatife dusulmez: elle duzeltilmis bir bakiye veya eski veri yuzunden
                // cikarilan gun kayitli kullanimdan fazla olabilir.
                balance.UsedDays = Math.Max(0m, balance.UsedDays - vacation.RequestedDays);
            }
        }

        vacation.Status = VacationStatus.Cancelled;
        vacation.ApprovedByUserId = currentUser.UserId;
        vacation.DecidedAt = clock.UtcNow;
        vacation.DecisionNote = string.IsNullOrWhiteSpace(request.DecisionNote)
            ? null
            : request.DecisionNote.Trim();

        // Tek SaveChanges = tek transaction: durum ve bakiye birlikte yazilir.
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(vacation.Id, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureCanCancel(Guid? employeeUserId)
    {
        if (HasPermission(Permissions.VacationsApprove))
        {
            return;
        }

        if (!HasPermission(Permissions.VacationsRequest))
        {
            throw new ForbiddenException(Messages.VacationCancelForbidden);
        }

        if (employeeUserId is null || employeeUserId != currentUser.UserId)
        {
            throw new ForbiddenException(Messages.VacationCancelOwnOnly);
        }
    }

    private bool HasPermission(string permission) =>
        currentUser.Permissions.Contains(permission, StringComparer.Ordinal);
}
