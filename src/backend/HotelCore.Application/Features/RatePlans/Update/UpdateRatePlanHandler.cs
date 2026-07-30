using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;

namespace HotelCore.Application.Features.RatePlans.Update;

/// <summary>
/// Fiyat planını günceller. Çakışma kontrolü <b>kendisi hariç tutularak</b> tekrarlanır
/// (bir plan kendi aralığıyla çakışamaz).
/// <para>
/// Geçmiş rezervasyonların tutarı yeniden hesaplanmaz: <c>Reservation.TotalAmount</c>
/// rezervasyon oluşturulurken/güncellenirken <b>dondurulmuş</b> bir değerdir. Fiyat planı
/// değişikliği geçmişe dönük olarak misafirin fiyatını değiştirmemelidir.
/// </para>
/// </summary>
internal sealed class UpdateRatePlanHandler(IAppDbContext database, RatePlanReader reader)
    : IRequestHandler<UpdateRatePlanRequest, RatePlanResponse>
{
    public async Task<RatePlanResponse> Handle(
        UpdateRatePlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);
        var isActive = request.IsActive ?? true;

        await reader.EnsureRoomTypeExistsAsync(request.RoomTypeId, plan.HotelId, cancellationToken)
            .ConfigureAwait(false);
        await reader.EnsureNoOverlapAsync(
                request.RoomTypeId,
                request.Channel,
                request.ValidFrom,
                request.ValidTo,
                isActive,
                excludeId: plan.Id,
                cancellationToken)
            .ConfigureAwait(false);

        plan.RoomTypeId = request.RoomTypeId;
        plan.Name = request.Name.Trim();
        plan.Price = request.Price;
        plan.ValidFrom = request.ValidFrom;
        plan.ValidTo = request.ValidTo;
        plan.Channel = request.Channel;
        plan.IsActive = isActive;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(plan.Id, cancellationToken).ConfigureAwait(false);
    }
}
