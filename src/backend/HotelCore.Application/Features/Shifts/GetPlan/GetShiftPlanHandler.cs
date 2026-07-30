using HotelCore.Application.Common.Interfaces;
using HotelCore.Application.Common.Messaging;
using HotelCore.Application.Features.Shifts.Common;

namespace HotelCore.Application.Features.Shifts.GetPlan;

internal sealed class GetShiftPlanHandler(ShiftReader reader, IDateTimeProvider clock)
    : IRequestHandler<GetShiftPlanRequest, ShiftPlanResponse>
{
    public Task<ShiftPlanResponse> Handle(
        GetShiftPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Varsayilan hafta sunucu saatinden (UTC) belirlenir; istemci saatine guvenilmez.
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var range = ShiftPlanRange.Resolve(request.Week, request.From, request.To, today);

        return reader.GetPlanAsync(range, cancellationToken);
    }
}
