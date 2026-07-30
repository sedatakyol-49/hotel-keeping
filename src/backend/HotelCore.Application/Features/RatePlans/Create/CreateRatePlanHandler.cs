using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.RatePlans.Common;
using HotelCore.Domain.Entities;

namespace HotelCore.Application.Features.RatePlans.Create;

/// <summary>
/// Yeni fiyat planı oluşturur. Oda tipi aktif otelde olmalıdır (404); aynı
/// <c>(RoomTypeId, Channel)</c> için tarih aralığı çakışan aktif bir plan varsa 409.
/// </summary>
internal sealed class CreateRatePlanHandler(
    IAppDbContext database,
    ICurrentUser currentUser,
    RatePlanReader reader)
    : IRequestHandler<CreateRatePlanRequest, RatePlanResponse>
{
    public async Task<RatePlanResponse> Handle(
        CreateRatePlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotelId = currentUser.RequireHotelId();
        var isActive = request.IsActive ?? true;

        await reader.EnsureRoomTypeExistsAsync(request.RoomTypeId, hotelId, cancellationToken)
            .ConfigureAwait(false);
        await reader.EnsureNoOverlapAsync(
                request.RoomTypeId,
                request.Channel,
                request.ValidFrom,
                request.ValidTo,
                isActive,
                excludeId: null,
                cancellationToken)
            .ConfigureAwait(false);

        var plan = new RatePlan
        {
            HotelId = hotelId,
            RoomTypeId = request.RoomTypeId,
            Name = request.Name.Trim(),
            Price = request.Price,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Channel = request.Channel,
            IsActive = isActive,
        };

        database.RatePlans.Add(plan);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(plan.Id, cancellationToken).ConfigureAwait(false);
    }
}
