using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Localization;
using HotelCore.Application.Common.Messaging;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.RoomTypes.Delete;

/// <summary>
/// Oda tipini soft-delete eder (<c>AppDbContext.SaveChanges</c> Deleted → Modified dönüşümünü yapar).
/// <para>
/// Bağlı <b>silinmemiş</b> oda varsa silme reddedilir (409): oda tipi olmayan oda kaydı
/// tutarsız olurdu ve FK davranışı zaten <c>Restrict</c>'tir. Silinmiş odalar global filter
/// sayesinde sayıma girmez. Çeviri satırları korunur — kayıt geri alınabilir olsun diye silinmez.
/// </para>
/// </summary>
internal sealed class DeleteRoomTypeHandler(IAppDbContext database)
    : IRequestHandler<DeleteRoomTypeRequest, Unit>
{
    public async Task<Unit> Handle(DeleteRoomTypeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await database.RoomTypes
            .FirstOrDefaultAsync(roomType => roomType.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(RoomType), request.Id);

        var hasRooms = await database.Rooms
            .AnyAsync(room => room.RoomTypeId == entity.Id, cancellationToken)
            .ConfigureAwait(false);

        if (hasRooms)
        {
            throw new ConflictException(Messages.RoomTypeHasRooms);
        }

        database.RoomTypes.Remove(entity);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
