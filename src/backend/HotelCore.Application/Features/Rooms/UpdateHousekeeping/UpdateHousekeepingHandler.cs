using HotelCore.Application.Common.Exceptions;
using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Rooms.Common;
using HotelCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelCore.Application.Features.Rooms.UpdateHousekeeping;

/// <summary>
/// Odanın kat hizmetleri durumunu (ve notunu) günceller.
/// <para>
/// <c>isOutOfOrder</c> bayrağı durumla birlikte tutarlı tutulur: durum <c>OutOfOrder</c> ise true,
/// aksi hâlde false (api-contracts.md). Not alanı gönderilmediyse/null ise temizlenir.
/// </para>
/// </summary>
internal sealed class UpdateHousekeepingHandler(IAppDbContext database, RoomReader reader)
    : IRequestHandler<UpdateHousekeepingRequest, RoomResponse>
{
    public async Task<RoomResponse> Handle(UpdateHousekeepingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await database.Rooms
            .FirstOrDefaultAsync(room => room.Id == request.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Room), request.Id);

        // isOutOfOrder istekte taşınmaz; mevcut bayrak değil YENİ durum belirleyicidir:
        // OutOfOrder'a geçiş true yapar, OutOfOrder'dan çıkış false yapar.
        HousekeepingState.Apply(entity, request.Status, isOutOfOrder: false);

        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await reader.GetAsync(entity.Id, cancellationToken).ConfigureAwait(false);
    }
}
