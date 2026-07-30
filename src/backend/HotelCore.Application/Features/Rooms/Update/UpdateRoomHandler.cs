using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Rooms.Update;

/// <summary>
/// Odayı günceller. Numara çakışması 409; oda bulunamazsa veya hedef oda tipi odanın oteline
/// ait değilse 404 döner.
/// </summary>
internal sealed class UpdateRoomHandler(IAppDbContext database, RoomReader reader)
    : IRequestHandler<UpdateRoomRequest, RoomResponse>
{
    public async Task<RoomResponse> Handle(UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await database.Rooms
            .FirstOrDefaultAsync(room => room.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), request.Id);

        // HotelId koşulu tenant filtresi değil, referans kapsamıdır: oda ile oda tipi
        // aynı otele ait olmalıdır (konsolide modda filtre bypass edildiğinde de geçerli).
        var roomTypeExists = await database.RoomTypes
            .AnyAsync(
                roomType => roomType.Id == request.RoomTypeId && roomType.HotelId == entity.HotelId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!roomTypeExists)
        {
            throw new NotFoundException(nameof(RoomType), request.RoomTypeId);
        }

        var number = request.Number.Trim();

        // Benzersizlik kapsamı: unique index (HotelId, Number).
        var numberExists = await database.Rooms
            .AnyAsync(
                room => room.Id != entity.Id
                        && room.HotelId == entity.HotelId
                        && room.Number == number,
                cancellationToken)
            .ConfigureAwait(false);

        if (numberExists)
        {
            throw new ConflictException(Messages.RoomNumberTaken(number));
        }

        entity.Number = number;
        entity.Floor = request.Floor;
        entity.RoomTypeId = request.RoomTypeId;
        entity.Note = request.Note?.Trim();

        HousekeepingState.Apply(entity, request.HousekeepingStatus, request.IsOutOfOrder);

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(entity.Id, cancellationToken).ConfigureAwait(false);
    }
}
