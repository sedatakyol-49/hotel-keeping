using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Shifts.Common;

namespace HotelCore.Application.Features.Shifts.Delete;

internal sealed class DeleteShiftHandler(IAppDbContext database, ShiftReader reader)
    : IRequestHandler<DeleteShiftRequest, Unit>
{
    public async Task<Unit> Handle(
        DeleteShiftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var shift = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        database.Shifts.Remove(shift);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
