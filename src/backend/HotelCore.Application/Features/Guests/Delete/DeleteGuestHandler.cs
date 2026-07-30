using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Guests.Common;

namespace HotelCore.Application.Features.Guests.Delete;

/// <summary>
/// Misafiri soft-delete eder. Geçmiş rezervasyonların misafir kaydına FK'si vardır
/// (<c>Restrict</c>), bu yüzden gerçek silme yapılmaz; kayıt yalnızca listelerden düşer.
/// </summary>
internal sealed class DeleteGuestHandler(IAppDbContext database, GuestReader reader)
    : IRequestHandler<DeleteGuestRequest, Unit>
{
    public async Task<Unit> Handle(DeleteGuestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guest = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        await reader.EnsureDeletableAsync(guest.Id, cancellationToken).ConfigureAwait(false);

        // AppDbContext Deleted -> Modified'a cevirip IsDeleted/DeletedAt damgalar.
        database.Guests.Remove(guest);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
