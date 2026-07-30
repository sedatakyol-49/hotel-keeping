using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Hr.Common;
using HotelCore.Application.Features.Vacations.Common;
using HotelCore.Domain.Enums;

namespace HotelCore.Application.Features.Vacations.Approve;

/// <summary>
/// İzin talebini onaylar ve ilgili yılın bakiyesini günceller (architecture.md §5).
/// <para>
/// <b>Atomiklik:</b> talebin durumu ile <c>VacationBalance.UsedDays</c> artışı <b>tek</b>
/// <c>SaveChangesAsync</c> çağrısında yazılır. EF Core bir SaveChanges'teki tüm komutları tek
/// transaction'da çalıştırdığı için "onaylandı ama bakiye düşmedi" (ya da tersi) durumu oluşamaz.
/// </para>
/// <para>
/// <b>Yıl seçimi:</b> gün sayısı talebin <c>From</c> tarihinin yılına yazılır. Yıl sonunu aşan
/// izinlerde (28.12 – 03.01) bölüştürme yapılmaz — bu bilinçli bir sadeleştirmedir; ayrıştırma
/// devir (<c>CarriedOverDays</c>) kurallarıyla birlikte ele alınmalıdır ve o kural bu fazda yok.
/// </para>
/// <para>
/// <b>Aktif otel gerekmez:</b> hedef satır zaten mevcuttur ve hangi otele ait olduğu bilinir;
/// bakiye çalışanın oteline yazılır. Bu yüzden konsolide moddaki Head Office kullanıcısı da
/// onay verebilir (yeni kayıt oluşturan uçlardan farkı budur).
/// </para>
/// </summary>
internal sealed class ApproveVacationHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    EmployeeLookup employees,
    VacationReader reader)
    : IRequestHandler<ApproveVacationRequest, VacationRequestResponse>
{
    public async Task<VacationRequestResponse> Handle(
        ApproveVacationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Yalnizca Pending onaylanabilir; ikinci onay -> 409 (bakiye iki kez artmaz).
        var vacation = await reader.GetPendingTrackedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        var employee = await employees.GetTrackedAsync(vacation.EmployeeId, cancellationToken)
            .ConfigureAwait(false);

        var balance = await reader
            .GetOrCreateBalanceAsync(employee, vacation.From.Year, cancellationToken)
            .ConfigureAwait(false);

        balance.UsedDays += vacation.RequestedDays;

        vacation.Status = VacationStatus.Approved;
        vacation.ApprovedByUserId = currentUser.UserId;
        vacation.DecidedAt = clock.UtcNow;
        vacation.DecisionNote = string.IsNullOrWhiteSpace(request.DecisionNote)
            ? null
            : request.DecisionNote.Trim();

        // Tek SaveChanges = tek transaction: durum ve bakiye birlikte yazilir.
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(vacation.Id, cancellationToken).ConfigureAwait(false);
    }
}
