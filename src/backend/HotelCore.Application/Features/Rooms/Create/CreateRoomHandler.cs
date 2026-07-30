using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Common.Security;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Entities;
using HotelCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Rooms.Create;

/// <summary>
/// Yeni oda oluşturur. <c>HotelId</c> aktif otelden gelir; oda numarası çakışırsa 409,
/// oda tipi bulunamazsa (veya başka otele aitse) 404 döner.
/// </summary>
internal sealed class CreateRoomHandler(IAppDbContext database, ICurrentUser currentUser, RoomReader reader)
    : IRequestHandler<CreateRoomRequest, RoomResponse>
{
    public async Task<RoomResponse> Handle(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hotelId = currentUser.RequireHotelId();
        var number = request.Number.Trim();

        // Oda tipi kapsam kontrolü: global filter başka otelin tipini zaten gizler; buradaki
        // HotelId koşulu Head Office konsolide modunda yanlış otele bağlamayı önler.
        var roomTypeExists = await database.RoomTypes
            .AnyAsync(
                roomType => roomType.Id == request.RoomTypeId && roomType.HotelId == hotelId,
                cancellationToken)
            .ConfigureAwait(false);

        if (!roomTypeExists)
        {
            throw new NotFoundException(nameof(RoomType), request.RoomTypeId);
        }

        // Benzersizlik aktif otel kapsamında; nihai güvence (HotelId, Number) unique index'i.
        var numberExists = await database.Rooms
            .AnyAsync(room => room.Number == number, cancellationToken)
            .ConfigureAwait(false);

        if (numberExists)
        {
            throw new ConflictException(Messages.RoomNumberTaken(number));
        }

        var entity = new Room
        {
            HotelId = hotelId,
            RoomTypeId = request.RoomTypeId,
            Number = number,
            Floor = request.Floor,
            Note = request.Note?.Trim()
        };

        HousekeepingState.Apply(
            entity,
            request.HousekeepingStatus ?? HousekeepingStatus.Clean,
            request.IsOutOfOrder ?? false);

        database.Rooms.Add(entity);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(entity.Id, cancellationToken).ConfigureAwait(false);
    }
}
