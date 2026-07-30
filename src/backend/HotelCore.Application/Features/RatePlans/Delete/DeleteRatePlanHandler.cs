using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.RatePlans.Common;

namespace HotelCore.Application.Features.RatePlans.Delete;

internal sealed class DeleteRatePlanHandler(IAppDbContext database, RatePlanReader reader)
    : IRequestHandler<DeleteRatePlanRequest, Unit>
{
    public async Task<Unit> Handle(DeleteRatePlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await reader.GetTrackedAsync(request.Id, cancellationToken).ConfigureAwait(false);

        await reader.EnsureDeletableAsync(plan.Id, cancellationToken).ConfigureAwait(false);

        database.RatePlans.Remove(plan);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
