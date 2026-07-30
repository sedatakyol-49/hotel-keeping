using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.TimeEntries.Common;

namespace HotelCore.Application.Features.TimeEntries.Delete;

internal sealed class DeleteTimeEntryHandler(IAppDbContext database, TimeEntryReader reader)
    : IRequestHandler<DeleteTimeEntryRequest, Unit>
{
    public async Task<Unit> Handle(
        DeleteTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entry = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        database.TimeEntries.Remove(entry);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
